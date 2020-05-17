using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ModixTranslator.HostedServices
{
    public class BotHostedService : IBotService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<BotHostedService> _logger;
        private readonly IHostApplicationLifetime _appLifetime;
        
        public DiscordSocketClient DiscordClient { get; private set; }

        public BotHostedService (IConfiguration config, ILogger<BotHostedService> logger, IHostApplicationLifetime appLifetime)
        {
            _config = config;
            _logger = logger;
            _appLifetime = appLifetime;

            // workaround to the compiler having no clue that StartAsync WILL be called by the platform
            DiscordClient = new DiscordSocketClient();
        }

        public void ExecuteHandlerAsyncronously<TReturn>(Func<DiscordSocketClient, Task<TReturn>> handler, Action<TReturn> callback)
        {
            if(DiscordClient.ConnectionState != ConnectionState.Connected)
            {
                _logger.LogWarning($"A handler attempted to execute a handler while the bot was disconnected"); 
                return;
            }

            // explicitly fire & forget the handler to prevent blocking the gateway;
            _ = handler(DiscordClient).ContinueWith(cb => callback(cb.Result));
        }

        private Task BotDisconnected(Exception arg)
        {
            if(arg is GatewayReconnectException)
            {
                return Task.CompletedTask;
            }

            _logger.LogCritical(arg, "Discord disconnected with a non-resumable error");
            _appLifetime.StopApplication();
            return Task.CompletedTask;
        }


        private Task BotLoggedIn()
        {
            _logger.LogInformation("Bot has logged into discord");
            return Task.CompletedTask;
        }

        private Task BotLog(LogMessage arg)
        {
            var message = $"{arg.Source}: {arg.Message}";
            if (arg.Exception is null)
            {
                _logger.Log(arg.Severity.ToLogLevel(), message);
            }
            else
            {
                _logger.Log(arg.Severity.ToLogLevel(), arg.Exception, message);
            }

            return Task.CompletedTask;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            DiscordClient = new DiscordSocketClient(new DiscordSocketConfig
            {
                AlwaysDownloadUsers = false,
                ConnectionTimeout = 30000,
                DefaultRetryMode = RetryMode.AlwaysRetry,
                GuildSubscriptions = true,
                HandlerTimeout = 1000,
                LogLevel = LogSeverity.Debug,
                MessageCacheSize = 1000,
                RateLimitPrecision = RateLimitPrecision.Millisecond,
                UseSystemClock = true
            });

            DiscordClient.LoggedIn += BotLoggedIn;
            DiscordClient.Disconnected += BotDisconnected;
            DiscordClient.Log += BotLog;

            var token = _config["DiscordToken"];
            await DiscordClient.LoginAsync(TokenType.Bot, token);
            await DiscordClient.StartAsync();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (DiscordClient is null)
            {
                return;
            }

            await DiscordClient.SetStatusAsync(UserStatus.Offline);
            await DiscordClient.StopAsync();
        }

        public ValueTask DisposeAsync()
        {
            DiscordClient?.Dispose();

            return new ValueTask();
        }
    }
}
