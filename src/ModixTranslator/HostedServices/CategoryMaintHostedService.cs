using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ModixTranslator.HostedServices
{
    public class CategoryMaintHostedService : IHostedService
    {
        private readonly ILogger<CategoryMaintHostedService> _logger;
        private readonly IBotService _bot;
        private readonly Timer _channelCleanupTimer;
        private readonly ConcurrentDictionary<ulong, DateTimeOffset> _channelActivity = new ConcurrentDictionary<ulong, DateTimeOffset>();

        public CategoryMaintHostedService(ILogger<CategoryMaintHostedService> logger, IBotService bot)
        {
            _logger = logger;
            _bot = bot;
            _channelCleanupTimer = new Timer(async a => await DoCleanup());
        }

        private async Task DoCleanup()
        {
            _logger.LogDebug("Begin cleanup of loc channels that are idle");
            foreach (var guild in _bot.DiscordClient.Guilds)
            {
                _logger.LogDebug($"Cleaning up {guild.Name}");
                var locCategory = guild.Channels.OfType<SocketCategoryChannel>().SingleOrDefault(a => a.Name == TranslationConstants.CategoryName);
                if (locCategory == null)
                {
                    continue;
                }

                _logger.LogDebug($"Cleaning up category {locCategory.Name} ({locCategory.Id})");

                var tempChannels = guild.GetCategoryChannel(locCategory.Id).Channels
                    .OfType<SocketTextChannel>()
                    .Where(a => !TranslationConstants.PermanentChannels.Contains(a.Name) && a.CategoryId == locCategory.Id);

                foreach (var channel in tempChannels)
                {
                    _logger.LogDebug($"Checking channel {channel.Name} to determine if its idle");
                    if (!_channelActivity.TryGetValue(channel.Id, out var lastMessageTime))
                    {
                        _logger.LogDebug("We haven't seen any recent messages from this channel, checking last message time");
                        var messageBatch = await channel.GetMessagesAsync(1).ToListAsync();
                        var messages = messageBatch.SelectMany(m => m.ToList()).ToList();

                        if (messages.Count == 0)
                        {
                            _logger.LogDebug("No messages retrieved, checking channel time");
                            lastMessageTime = channel.CreatedAt;
                        }
                        else
                        {
                            var latestTimestamp = messages.Max(b => b.CreatedAt);
                            var message = messages.FirstOrDefault(a => a.CreatedAt == latestTimestamp);
                            if (message == default)
                            {
                                _logger.LogDebug("No messages retrieved in batch, checking channel time");
                                lastMessageTime = channel.CreatedAt;
                            }
                            else
                            {
                                _logger.LogDebug($"Last message found, sent at: {message.CreatedAt}");
                                lastMessageTime = message.CreatedAt;
                            }
                        }
                    }

                    if (lastMessageTime == DateTimeOffset.MinValue)
                    {
                        _logger.LogWarning("Unable to determine if the channel is idle");
                        continue;
                    }

                    if ((DateTimeOffset.UtcNow - lastMessageTime).TotalMinutes >= 60)
                    {
                        _logger.LogDebug($"Channel {channel.Name} is idle, deleting.");
                        await channel.DeleteAsync();
                    }
                    else
                    {
                        _logger.LogDebug($"Channel {channel.Name} is not idle");
                    }
                }
            }

            _logger.LogDebug("Completed cleanup of loc channels that are idle");
        }

        private Task BotMessageReceived(SocketMessage arg)
        {
            if (!(arg.Channel is SocketTextChannel channel) || channel.Category == null)
            {
                return Task.CompletedTask;
            }
            var localizedCategory = channel.Guild.CategoryChannels.SingleOrDefault(a => a.Name == TranslationConstants.CategoryName);
            if (localizedCategory == null)
            {
                return Task.CompletedTask;
            }

            if (channel.Category.Id == localizedCategory.Id)
            {
                _channelActivity[channel.Id] = channel.CreatedAt;
            }

            return Task.CompletedTask;
        }

        private Task BotChannelDeleted(SocketChannel arg)
        {
            _channelActivity.TryRemove(arg.Id, out _);
            return Task.CompletedTask;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _channelCleanupTimer.Change(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
            _bot.DiscordClient.MessageReceived += BotMessageReceived;
            _bot.DiscordClient.ChannelDestroyed += BotChannelDeleted;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _channelCleanupTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _channelCleanupTimer.Dispose();

            _bot.DiscordClient.MessageReceived -= BotMessageReceived;
            _bot.DiscordClient.ChannelDestroyed -= BotChannelDeleted;
            return Task.CompletedTask;
        }
    }
}
