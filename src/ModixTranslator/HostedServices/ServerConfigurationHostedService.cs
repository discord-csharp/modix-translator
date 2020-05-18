using Discord;
using Discord.Rest;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModixTranslator.Behaviors;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ModixTranslator.HostedServices
{
    public interface IServerConfigurationService : IHostedService
    {
        string GetLanguageForGuild(ulong guildId);
    }

    public class ServerConfigurationHostedService : IServerConfigurationService
    {
        private readonly ILogger<ServerConfigurationHostedService> _logger;
        private readonly IBotService _bot;
        private readonly ITranslationService _translationService;
        private readonly AutoResetEvent _channelCreatedWaiter;
        private string _waitingForChannel = string.Empty;
        private readonly Dictionary<ulong, string> _guildLanguages = new Dictionary<ulong, string>();

        public ServerConfigurationHostedService(ILogger<ServerConfigurationHostedService> logger, IBotService bot, ITranslationService translationService)
        {
            _logger = logger;
            _bot = bot;
            _translationService = translationService;
            _channelCreatedWaiter = new AutoResetEvent(false);
        }

        public string GetLanguageForGuild(ulong guildId)
        {
            if(!_guildLanguages.TryGetValue(guildId, out var lang))
            {
                return TranslationConstants.StandardLanguage;
            }

            return lang;
        }

        private Task OnConfigureGuild(SocketGuild guild)
        {
            _bot.ExecuteHandlerAsyncronously(handler: async bot =>
            {
                await ConfigureGuild(guild);
                return new object();
            },
            callback: (result) => { });
            return Task.CompletedTask;
        }

        private async Task ConfigureGuild(SocketGuild guild)
        {
            _logger.LogDebug($"Configuring guild {guild.Name}");

            _logger.LogDebug($"Detecting language for guild {guild.Name}");
            var guildLang = guild.PreferredLocale;

            _logger.LogDebug($"PreferredLocale set to {guildLang}");
            var ci = new CultureInfo(guildLang);
            guildLang = ci.Parent?.Name;
            if (string.IsNullOrEmpty(guildLang))
            {
                guildLang = ci.Name;
            }

            if(!await _translationService.IsLangSupported(guildLang))
            {
                _logger.LogDebug($"Couldn't resolve guild language {guildLang}, defaulting to {TranslationConstants.StandardLanguage}");
                guildLang = TranslationConstants.StandardLanguage;
            }

            _logger.LogDebug($"Setting the translation language for {guild.Name} to {guildLang}");
            _guildLanguages[guild.Id] = guildLang;

            _logger.LogDebug(string.Join(", ", guild.CategoryChannels.Select(a => a.Name)));

            var category = guild.CategoryChannels.SingleOrDefault(a => a.Name == TranslationConstants.CategoryName) as ICategoryChannel;
            if (category == null)
            {
                _logger.LogDebug($"'{TranslationConstants.CategoryName}' category not found, creating.");
                _waitingForChannel = TranslationConstants.CategoryName;
                category = await guild.CreateCategoryChannelAsync(TranslationConstants.CategoryName);
                _channelCreatedWaiter.WaitOne();
            }

            var tmpCat = guild.GetCategoryChannel(category.Id);
            var tmpChans = tmpCat.Channels;

            var howtoChannel = await CreateOrUpdateChannel(guild, category, TranslationConstants.HowToChannelName, $"Use the ??translate create <your-language> command to start a session", 999);
            await PostStockMessages(howtoChannel);
            await CreateOrUpdateChannel(guild, category, TranslationConstants.HistoryChannelName, $"Use this channel to search past localized conversations", 0);

            _logger.LogDebug($"Done configuring guild {guild.Name}");
        }

        private async Task PostStockMessages(IGuildChannel howtoChannel)
        {
            var channel = await howtoChannel.Guild.GetTextChannelAsync(howtoChannel.Id);
            var messageBatch = await channel.GetMessagesAsync(20).ToListAsync();
            var messages = messageBatch.SelectMany(m => m.ToList()).ToList();

            var supportedMessage = messages.FirstOrDefault(a => a.Content.Contains("Supported Languages:"));
            if (supportedMessage == null)
            {
                var messageBuilder = new StringBuilder();
                var langs = await _translationService.GetSupportedLanguages();
                messageBuilder.Append($"{Format.Bold("Supported Languages:")}\n");
                messageBuilder.Append("```\n");
                messageBuilder.Append($"{"Language",-9}Name\n");

                foreach (var lang in langs.Translation.OrderBy(a => a.Key))
                {
                    messageBuilder.Append($"{lang.Key,-9}{lang.Value.NativeName}\n");
                }
                messageBuilder.Append("```");

                await channel.SendMessageAsync(messageBuilder.ToString());
            }

            var commandMessage = messages.FirstOrDefault(a => a.Content.Contains("Usage:"));
            if (commandMessage == null)
            {
                await channel.SendMessageAsync($"{Format.Bold("Usage:")} {Format.Code("??translate create <lang>")}");
            }

            var exampleMessage = messages.FirstOrDefault(a => a.Content.Contains("Example:"));
            if (exampleMessage == null)
            {
                await channel.SendMessageAsync($"{Format.Bold("Example:")} {Format.Code("??translate create es")}");
            }

        }

        private async Task<IGuildChannel> CreateOrUpdateChannel(SocketGuild guild, ICategoryChannel category, string name, string topic, int position)
        {
            var channel = guild.Channels.OfType<SocketTextChannel>().SingleOrDefault(a => a.Name == name && a.CategoryId == category.Id) as IGuildChannel;
            if (channel == null)
            {
                _logger.LogDebug($"'#{name}' channel not found, creating.");
                _waitingForChannel = name;
                channel = await guild.CreateTextChannelAsync(name, a =>
                {
                    a.CategoryId = category.Id;
                    a.Position = position;
                    a.Topic = topic;
                });
                _channelCreatedWaiter.WaitOne();
            }

            _logger.LogDebug($"Enforcing deny to send messages, or reactions to @everyone in the '#{name}' channel");
            await channel.AddPermissionOverwriteAsync(guild.EveryoneRole, new OverwritePermissions(sendMessages: PermValue.Deny, addReactions: PermValue.Deny));
            var botRole = guild.Roles.SingleOrDefault(a => a.Name == _bot.DiscordClient.CurrentUser.Username);
            if (botRole != null)
            {
                await channel.AddPermissionOverwriteAsync(botRole, new OverwritePermissions(sendMessages: PermValue.Allow, addReactions: PermValue.Allow));
            }
            else
            {
                await channel.AddPermissionOverwriteAsync(_bot.DiscordClient.CurrentUser, new OverwritePermissions(sendMessages: PermValue.Allow, addReactions: PermValue.Allow));
            }
            return channel;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _bot.DiscordClient.GuildAvailable += OnConfigureGuild;
            _bot.DiscordClient.JoinedGuild += OnConfigureGuild;
            _bot.DiscordClient.ChannelCreated += ChannelCreated;

            return Task.CompletedTask;
        }

        private Task ChannelCreated(SocketChannel arg)
        {
            if ((arg is SocketCategoryChannel category && category.Name == _waitingForChannel)
                || (arg is SocketTextChannel channel && channel.Name == _waitingForChannel))
            {
                _channelCreatedWaiter.Set();
            }
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _bot.DiscordClient.GuildAvailable -= OnConfigureGuild;
            _bot.DiscordClient.JoinedGuild -= OnConfigureGuild;
            return Task.CompletedTask;
        }
    }
}
