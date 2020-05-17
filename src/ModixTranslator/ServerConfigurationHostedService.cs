using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TranslatorBot9000
{
    public class ServerConfigurationHostedService : IHostedService
    {
        private readonly ILogger<ServerConfigurationHostedService> _logger;
        private readonly IBotService _bot;
        private readonly AutoResetEvent _channelCreatedWaiter;
        private string _waitingForChannel = string.Empty;

        public ServerConfigurationHostedService(ILogger<ServerConfigurationHostedService> logger, IBotService bot)
        {
            _logger = logger;
            _bot = bot;
            _channelCreatedWaiter = new AutoResetEvent(false);
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

            _logger.LogDebug(string.Join(", ", guild.CategoryChannels.Select(a => a.Name)));

            var category = guild.CategoryChannels.SingleOrDefault(a => a.Name == LocalizationConstants.CategoryName) as ICategoryChannel;
            if (category == null)
            {
                _logger.LogDebug($"'{LocalizationConstants.CategoryName}' category not found, creating.");
                _waitingForChannel = LocalizationConstants.CategoryName;
                category = await guild.CreateCategoryChannelAsync(LocalizationConstants.CategoryName);
                _channelCreatedWaiter.WaitOne();
            }
            
            var tmpCat = guild.GetCategoryChannel(category.Id);
            var tmpChans = tmpCat.Channels;

            await CreateOrUpdateChannel(guild, category, LocalizationConstants.HowToChannelName, $"Use the ??localize <your-language> command to start a session", 999);
            await CreateOrUpdateChannel(guild, category, LocalizationConstants.HistoryChannelName, $"Use this channel to search past localized conversations", 0);

            _logger.LogDebug($"Done configuring guild {guild.Name}");
        }

        private async Task CreateOrUpdateChannel(SocketGuild guild, ICategoryChannel category, string name, string topic, int position)
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
