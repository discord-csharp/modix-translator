using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TranslatorBot9000
{
    public interface ILocalizerHostedService : IHostedService
    {
        Task<ChannelPair?> GetOrCreateChannelPair(SocketGuild guild, string lang);
    }

    public class LocalizerHostedService : ILocalizerHostedService
    {
        private readonly ILogger<LocalizerHostedService> _logger;
        private readonly ITranslationService _translation;
        private readonly IBotService _bot;
        private readonly ConcurrentDictionary<string, ChannelPair> _channelPairs = new ConcurrentDictionary<string, ChannelPair>();

        public LocalizerHostedService(ILogger<LocalizerHostedService> logger, ITranslationService translationService, IBotService bot)
        {
            _logger = logger;
            _translation = translationService;
            _bot = bot;
        }

        public async Task<ChannelPair?> GetOrCreateChannelPair(SocketGuild guild, string lang)
        {
            string safeLang = GetSafeLangString(lang);
            if (_channelPairs.TryGetValue(safeLang, out var pair))
            {
                return pair;
            }

            var category = guild.CategoryChannels.SingleOrDefault(a => a.Name == LocalizationConstants.CategoryName);
            if (category == default)
            {
                throw new InvalidOperationException($"The channel category {LocalizationConstants.CategoryName} does not exist");
            }

            var supportedLang = await _translation.IsLangSupported(lang);

            if (!supportedLang)
            {
                throw new LanguageNotSupportedException($"{lang} is not supported at this time.");
            }

            var fromLangName = $"from-{safeLang}-to-en";
            var toLangName = $"to-{safeLang}-from-en";
            var fromLangTopic = await _translation.GetTranslation("en", lang, $"Type naturally in your native language. Responses will be translated to English and posted in this channel's pair #from-en-to-{lang}");
            var toLangTopic = $"Type naturally in English. Responses will be translated to {lang} and posted in this channel's pair #from-{lang}-to-en";

            var fromLangChannel = await guild.CreateTextChannelAsync(fromLangName, p =>
            {
                p.CategoryId = category.Id;
                p.Topic = fromLangTopic;
            });

            var ToLangChannel = await guild.CreateTextChannelAsync(toLangName, p =>
            {
                p.CategoryId = category.Id;
                p.Topic = toLangTopic;
            });
            pair = new ChannelPair
            {
                LangChannel = fromLangChannel,
                EnglishChannel = ToLangChannel
            };

            if (!_channelPairs.TryAdd(safeLang, pair))
            {
                _logger.LogWarning($"The channel pairs {{{fromLangName}, {toLangName}}} have already been tracked, cleaning up");
                await pair.LangChannel.DeleteAsync();
                await pair.EnglishChannel.DeleteAsync();
                _channelPairs.TryGetValue(safeLang, out pair);
            }

            return pair;
        }

        private static string GetSafeLangString(string lang)
        {
            return lang.ToLower().Replace("-", "_");
        }

        private string? GetLangFromChannelName(string channelName)
        {
            var nameParts = channelName.Split('-');
            if (nameParts.Length >= 2)
            {
                return nameParts[1].Replace("_", "-");
            }
            _logger.LogWarning($"{channelName} is not the expected format.");
            return null;
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

            if (messageChannel.Category.Name != LocalizationConstants.CategoryName)
            {
                return Task.CompletedTask;
            }

            //todo:
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

            if (messageChannel.Category.Name != LocalizationConstants.CategoryName)
            {
                return Task.CompletedTask;
            }

            if (LocalizationConstants.PermanentChannels.Contains(messageChannel.Name))
            {
                return Task.CompletedTask;
            }

            var lang = GetLangFromChannelName(messageChannel.Name);

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

            _bot.ExecuteHandlerAsyncronously<(SocketCategoryChannel category, string original, string translated)>(
                handler: async discord =>
                {
                    if(pair?.LangChannel == null || pair?.EnglishChannel == null)
                    {
                        throw new InvalidOperationException("Invalid channel pair");
                    }

                    var relayText = string.Empty;
                    if (messageChannel.Id == pair.EnglishChannel.Id)
                    {
                        _logger.LogDebug($"Message received from english channel {messageChannel.Name}, sending to {pair.LangChannel.Name}");
                        if (!string.IsNullOrWhiteSpace(message.Content))
                        {
                            relayText = await _translation.GetTranslation("en", lang, message.Content);
                        }

                        if(message.Attachments.Count != 0)
                        {
                            relayText += $" {string.Join(" ", message.Attachments.Select(a => a.Url))}";
                        }
                        await pair.LangChannel.SendMessageAsync($"{guildUser.Nickname ?? guildUser.Username}: {relayText}");
                    }
                    else if (messageChannel.Id == pair.LangChannel.Id)
                    {
                        _logger.LogDebug($"Message received from localized channel channel {messageChannel.Name}, sending to {pair.EnglishChannel.Name}");
                        if (!string.IsNullOrWhiteSpace(message.Content))
                        {
                            relayText = await _translation.GetTranslation(lang, "en", message.Content);
                        }

                        if (message.Attachments.Count != 0)
                        {
                            relayText += $" {string.Join(" ", message.Attachments.Select(a => a.Url))}";
                        }

                        await pair.EnglishChannel.SendMessageAsync($"{guildUser.Nickname ?? guildUser.Username}: {relayText}");
                    }

                    return (categoryChannel, message.Content, relayText);
                },
                callback: async result =>
                {
                    var historyChannel = result.category.Channels.OfType<SocketTextChannel>()
                        .SingleOrDefault(a => a.Name == LocalizationConstants.HistoryChannelName);
                    if (historyChannel == null)
                    {
                        return;
                    }
                    _logger.LogDebug("Sending messages to the history channel");

                    await historyChannel.SendMessageAsync($"{guildUser.Nickname ?? guildUser.Username}: {result.original}");
                    await historyChannel.SendMessageAsync($"{guildUser.Nickname ?? guildUser.Username}: {result.translated}");


                    _logger.LogDebug("Completed translating messages");
                });

            return Task.CompletedTask;
        }


        private Task ChannelDeleted(SocketChannel channel)
        {
            string foundLang = string.Empty;
            foreach (var pair in _channelPairs)
            {
                if(pair.Value?.LangChannel == null || pair.Value?.EnglishChannel == null)
                {
                    _logger.LogWarning("invalid channel pair detected");
                    continue;
                }

                if (channel.Id == pair.Value.EnglishChannel.Id)
                {
                    _logger.LogDebug($"English channel {pair.Value.EnglishChannel.Name} deleted, removing paired lang channel {pair.Value.LangChannel.Name}");
                    foundLang = pair.Key;
                }
                else if (channel.Id == pair.Value.LangChannel.Id)
                {
                    _logger.LogDebug($"English channel {pair.Value.LangChannel.Name} deleted, removing paired lang channel {pair.Value.EnglishChannel.Name}");
                    foundLang = pair.Key;
                }
            }

            if (foundLang != string.Empty)
            {
                _channelPairs.TryRemove(foundLang, out _);
            }

            return Task.CompletedTask;
        }

        private Task GuildAvailable(SocketGuild guild)
        {
            var category = guild.CategoryChannels.SingleOrDefault(a => a.Name == LocalizationConstants.CategoryName);
            if (category == null)
            {
                return Task.CompletedTask;
            }

            _logger.LogDebug($"Guild available for {guild.Name}, rebuilding pair map");
            var tempChannels = category.Channels.OfType<ITextChannel>().Where(a => !LocalizationConstants.PermanentChannels.Contains(a.Name)).ToList();

            if (tempChannels.Count == 0)
            {
                return Task.CompletedTask;
            }

            var pairs = new Dictionary<string, ChannelPair>();

            foreach (var channel in tempChannels)
            {
                _logger.LogDebug($"Checking {channel.Name}");
                var lang = GetLangFromChannelName(channel.Name);
                if (lang == null)
                {
                    _logger.LogDebug($"{channel.Name} is not a translation channel, skipping");
                    continue;
                }

                var safeLang = GetSafeLangString(lang);
                var isEnglishChannel = channel.Name.StartsWith("to");
                _logger.LogDebug($"channel is the english lang channel? {isEnglishChannel}");

                if (!pairs.TryGetValue(safeLang, out var pair))
                {
                    _logger.LogDebug("Creating new pair");
                    pair = new ChannelPair();
                    pairs[safeLang] = pair;
                }

                if (isEnglishChannel)
                {
                    pair.EnglishChannel = channel;
                }
                else
                {
                    pair.LangChannel = channel;
                }
            }

            foreach (var pair in pairs.ToList())
            {
                if (pair.Value.EnglishChannel == default || pair.Value.LangChannel == default)
                {
                    _logger.LogDebug("Pair is missing either the language channel or the english channel, skipping");
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
            _bot.DiscordClient.ChannelDestroyed += ChannelDeleted;

            if (_bot.DiscordClient.ConnectionState == ConnectionState.Connected && _bot.DiscordClient.Guilds.Count > 0)
            {
                _logger.LogDebug("Discord bot is already connected, rebuilding pair map");
                foreach (var guild in _bot.DiscordClient.Guilds)
                {
                    await GuildAvailable(guild);
                }
            }
            else
            {
                _logger.LogDebug("Discord bot has not connected, registering the GuildAvailable event");
                _bot.DiscordClient.GuildAvailable += GuildAvailable;
            }

            _logger.LogDebug("Localizer started");
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _bot.DiscordClient.MessageReceived -= MessageReceived;
            _bot.DiscordClient.MessageUpdated -= MessageUpdated;
            _bot.DiscordClient.ChannelDestroyed -= ChannelDeleted;
            _bot.DiscordClient.GuildAvailable -= GuildAvailable;
            return Task.CompletedTask;
        }
    }
}
