using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using ModixTranslator.Behaviors;
using ModixTranslator.Models.Translator;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ModixTranslator.HostedServices
{
    public class TranslatorHostedService : ITranslatorHostedService
    {
        private readonly ILogger<TranslatorHostedService> _logger;
        private readonly ITranslationService _translation;
        private readonly IBotService _bot;
        private readonly IServerConfigurationService _serverConfig;
        private readonly ConcurrentDictionary<string, ChannelPair> _channelPairs = new ConcurrentDictionary<string, ChannelPair>();

        public TranslatorHostedService(ILogger<TranslatorHostedService> logger, ITranslationService translationService, IBotService bot, IServerConfigurationService serverConfig)
        {
            _logger = logger;
            _translation = translationService;
            _bot = bot;
            _serverConfig = serverConfig;
        }

        public async Task<ChannelPair?> GetOrCreateChannelPair(SocketGuild guild, string lang)
        {
            string safeLang = GetSafeLangString(lang);
            string guildLang = _serverConfig.GetLanguageForGuild(guild.Id);
            string safeGuildLang = GetSafeLangString(guildLang);
            if (_channelPairs.TryGetValue(safeLang, out var pair))
            {
                return pair;
            }

            var category = guild.CategoryChannels.SingleOrDefault(a => a.Name == TranslationConstants.CategoryName);
            if (category == default)
            {
                throw new InvalidOperationException($"The channel category {TranslationConstants.CategoryName} does not exist");
            }

            var supportedLang = await _translation.IsLangSupported(lang);

            if (!supportedLang)
            {
                throw new LanguageNotSupportedException($"{lang} is not supported at this time.");
            }

            var fromLangName = $"{safeLang}-to-{safeGuildLang}";
            var toLangName = $"{safeGuildLang}-to-{safeLang}";

            var fromLangChannel = await guild.CreateTextChannelAsync(fromLangName, p => p.CategoryId = category.Id);
            var toLangChannel = await guild.CreateTextChannelAsync(toLangName, p => p.CategoryId = category.Id);

            var localizedTopic = await _translation.GetTranslation(guildLang, lang, $"Responses will be translated to {guildLang} and posted in this channel's pair {toLangChannel.Mention}");
            await fromLangChannel.ModifyAsync(p => p.Topic = localizedTopic);

            var unlocalizedTopic = $"Responses will be translated to {lang} and posted in this channel's pair {fromLangChannel.Mention}";
            await toLangChannel.ModifyAsync(p => p.Topic = unlocalizedTopic);

            pair = new ChannelPair
            {
                TranslationChannel = fromLangChannel,
                StandardLangChanel = toLangChannel
            };

            if (!_channelPairs.TryAdd(safeLang, pair))
            {
                _logger.LogWarning($"The channel pairs {{{fromLangName}, {toLangName}}} have already been tracked, cleaning up");
                await pair.TranslationChannel.DeleteAsync();
                await pair.StandardLangChanel.DeleteAsync();
                _channelPairs.TryGetValue(safeLang, out pair);
            }

            return pair;
        }

        private static string GetSafeLangString(string lang)
        {
            return lang.ToLower().Replace("-", "_");
        }

        private Task MessageUpdated(Cacheable<IMessage, ulong> lastMessage, SocketMessage newMessage, ISocketMessageChannel channel)
        {
            if (!(newMessage.Channel is SocketTextChannel messageChannel))
            {
                return Task.CompletedTask;
            }

            if (messageChannel.Category == null)
            {
                return Task.CompletedTask;
            }

            if (messageChannel.Category.Name != TranslationConstants.CategoryName)
            {
                return Task.CompletedTask;
            }

            //todo: message editing
            return Task.CompletedTask;
        }

        private Task MessageReceived(SocketMessage message)
        {
            if (message.Author.Id == _bot.DiscordClient.CurrentUser.Id)
            {
                return Task.CompletedTask;
            }

            if (!(message.Author is SocketGuildUser guildUser))
            {
                return Task.CompletedTask;
            }

            if (!(message.Channel is SocketTextChannel messageChannel))
            {
                return Task.CompletedTask;
            }

            if (!(messageChannel.Category is SocketCategoryChannel categoryChannel))
            {
                return Task.CompletedTask;
            }

            if (messageChannel.Category.Name != TranslationConstants.CategoryName)
            {
                return Task.CompletedTask;
            }

            if (TranslationConstants.PermanentChannels.Contains(messageChannel.Name))
            {
                return Task.CompletedTask;
            }

            var guildLang = _serverConfig.GetLanguageForGuild(messageChannel.Guild.Id);
            var safeGuildLang = GetSafeLangString(guildLang);
            var lang = messageChannel.GetLangFromChannelName(safeGuildLang);

            if (lang == null)
            {
                return Task.CompletedTask;
            }

            var safeLang = GetSafeLangString(lang);

            if (!_channelPairs.TryGetValue(safeLang, out var pair))
            {
                _logger.LogWarning("Message received from a loc channel without a valid pair");
                return Task.CompletedTask;
            }

            _logger.LogDebug("Starting translation of message");

            _bot.ExecuteHandlerAsyncronously<(SocketCategoryChannel category, Translation? guildLangTranslation, Translation?
                langTranslation)>(
                handler: async discord =>
                {
                    Translation? guildLangTranslation = null;
                    Translation? langTranslation = null;

                    if (pair?.TranslationChannel == null || pair?.StandardLangChanel == null)
                    {
                        throw new InvalidOperationException("Invalid channel pair");
                    }

                    string relayText = string.Empty;
                    if (messageChannel.Id == pair.StandardLangChanel.Id)
                    {
                        relayText = await SendMessageToPartner(message, $"{guildUser.Nickname ?? guildUser.Username}", pair.TranslationChannel, guildLang, lang);

                        guildLangTranslation = new Translation(message.Content, guildLang);
                        langTranslation = new Translation(relayText, lang);
                    }
                    else if (messageChannel.Id == pair.TranslationChannel.Id)
                    {
                        relayText = await SendMessageToPartner(message, $"{guildUser.Nickname ?? guildUser.Username}", pair.StandardLangChanel, lang, guildLang);


                        guildLangTranslation = new Translation(relayText, guildLang);
                        langTranslation = new Translation(message.Content, lang);
                    }

                    return (categoryChannel, guildLangTranslation, langTranslation);
                },
                callback: async result =>
                {
                    if (result.category == null || string.IsNullOrWhiteSpace(result.guildLangTranslation?.Text) || string.IsNullOrWhiteSpace(result.langTranslation?.Text))
                    {
                        return;
                    }

                    var historyChannel = result.category.Channels.OfType<SocketTextChannel>()
                        .SingleOrDefault(a => a.Name == TranslationConstants.HistoryChannelName);
                    if (historyChannel == null)
                    {
                        return;
                    }

                    _logger.LogDebug("Sending messages to the history channel");

                    var nickname = guildUser.Nickname ?? guildUser.Username;
                    var avatar = guildUser.GetAvatarUrl() ?? guildUser.GetDefaultAvatarUrl();

                    var embed = new EmbedBuilder()
                        .WithAuthor(nickname, avatar)
                        .AddField(result.guildLangTranslation.Language, result.guildLangTranslation.Text, true)
                        .AddField(result.langTranslation.Language, result.langTranslation.Text, true)
                        .Build();

                    await historyChannel.SendMessageAsync(embed: embed);

                    _logger.LogDebug("Completed translating messages");
                });

            return Task.CompletedTask;
        }

        private async Task<string> SendMessageToPartner(SocketMessage message, string username, ITextChannel targetChannel, string from, string to)
        {
            var guildLang = _serverConfig.GetLanguageForGuild(targetChannel.Guild.Id);
            var safeGuildLang = GetSafeLangString(guildLang);
            var isStandardLang = targetChannel.IsStandardLangChannel(safeGuildLang);

            _logger.LogDebug($"Message received from {from} channel '{message.Channel.Name}', sending to {targetChannel.Name}");
            string relayText = string.Empty;
            if (!string.IsNullOrWhiteSpace(message.Content))
            {
                relayText = await _translation.GetTranslation(from, to, message.Content);
            }

            if (message.Attachments.Count != 0)
            {
                relayText += $" {string.Join(" ", message.Attachments.Select(a => a.Url))}";
            }

            await targetChannel.SendMessageAsync($"{Format.Bold(username)}: {relayText}");
            return relayText;
        }

        private Task RemoveChannelFromMap(SocketChannel channel)
        {
            string foundLang = string.Empty;
            foreach (var pair in _channelPairs)
            {
                if (pair.Value?.TranslationChannel == null || pair.Value?.StandardLangChanel == null)
                {
                    _logger.LogWarning("invalid channel pair detected");
                    continue;
                }

                if (channel.Id == pair.Value.StandardLangChanel.Id || channel.Id == pair.Value.TranslationChannel.Id)
                {
                    foundLang = pair.Key;
                }
            }

            if (foundLang != string.Empty)
            {
                _logger.LogDebug($"One of the channels in a pair were deleted, removing pair '{foundLang}' from map");
                _channelPairs.TryRemove(foundLang, out _);
            }

            return Task.CompletedTask;
        }

        private Task BuildChannelMap(SocketGuild guild)
        {
            var category = guild.CategoryChannels.SingleOrDefault(a => a.Name == TranslationConstants.CategoryName);
            if (category == null)
            {
                return Task.CompletedTask;
            }

            _logger.LogDebug($"Guild available for {guild.Name}, rebuilding pair map");
            var tempChannels = category.Channels.OfType<ITextChannel>().Where(a => !TranslationConstants.PermanentChannels.Contains(a.Name)).ToList();

            if (tempChannels.Count == 0)
            {
                return Task.CompletedTask;
            }

            var pairs = new Dictionary<string, ChannelPair>();

            var guildLang = _serverConfig.GetLanguageForGuild(guild.Id);
            var safeGuildLang = GetSafeLangString(guildLang);

            foreach (var channel in tempChannels)
            {
                _logger.LogDebug($"Checking {channel.Name}");
                var lang = channel.GetLangFromChannelName(safeGuildLang);
                if (lang == null)
                {
                    _logger.LogDebug($"{channel.Name} is not a translation channel, skipping");
                    continue;
                }

                var safeLang = GetSafeLangString(lang);
                var isStandardLangChannel = channel.IsStandardLangChannel(safeGuildLang);
                _logger.LogDebug($"channel is the {safeGuildLang} lang channel? {isStandardLangChannel}");

                if (!pairs.TryGetValue(safeLang, out var pair))
                {
                    _logger.LogDebug("Creating new pair");
                    pair = new ChannelPair();
                    pairs[safeLang] = pair;
                }

                if (isStandardLangChannel)
                {
                    pair.StandardLangChanel = channel;
                }
                else
                {
                    pair.TranslationChannel = channel;
                }
            }

            foreach (var pair in pairs.ToList())
            {
                if (pair.Value.StandardLangChanel == default || pair.Value.TranslationChannel == default)
                {
                    _logger.LogDebug($"Pair is missing either the language channel or the {safeGuildLang} channel, skipping");
                    continue;
                }

                _logger.LogDebug($"Addping pair for {pair.Key}");
                _channelPairs[pair.Key] = pair.Value;
            }
            _logger.LogDebug($"Completed rebuilding pair map");
            return Task.CompletedTask;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogDebug("Localizer starting up");
            _bot.DiscordClient.MessageReceived += MessageReceived;
            _bot.DiscordClient.MessageUpdated += MessageUpdated;
            _bot.DiscordClient.ChannelDestroyed += RemoveChannelFromMap;

            if (_bot.DiscordClient.ConnectionState == ConnectionState.Connected && _bot.DiscordClient.Guilds.Count > 0)
            {
                _logger.LogDebug("Discord bot is already connected, rebuilding pair map");
                foreach (var guild in _bot.DiscordClient.Guilds)
                {
                    await BuildChannelMap(guild);
                }
            }
            else
            {
                _logger.LogDebug("Discord bot has not connected, registering the GuildAvailable event");
                _bot.DiscordClient.GuildAvailable += BuildChannelMap;
            }

            _logger.LogDebug("Localizer started");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _bot.DiscordClient.MessageReceived -= MessageReceived;
            _bot.DiscordClient.MessageUpdated -= MessageUpdated;
            _bot.DiscordClient.ChannelDestroyed -= RemoveChannelFromMap;
            _bot.DiscordClient.GuildAvailable -= BuildChannelMap;
            return Task.CompletedTask;
        }
    }
}
