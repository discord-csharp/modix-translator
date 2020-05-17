using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ModixTranslator.HostedServices
{
    public class HostedCommandHostedService : IHostedService
    {
        private readonly ILogger<HostedCommandHostedService> _logger;
        private readonly IServiceProvider _provider;
        private readonly IBotService _botService;
        private readonly CommandService _commandService;

        public HostedCommandHostedService(ILogger<HostedCommandHostedService> logger, IServiceProvider provider, IBotService botService, CommandService commandService)
        {
            _logger = logger;
            _provider = provider;
            _botService = botService;
            _commandService = commandService;
        }

        private Task LogCommand(LogMessage arg)
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

        private Task BotMessageReceived(SocketMessage message)
        {
            if (message.Author.IsBot)
            {
                return Task.CompletedTask;
            }

            if (!(message is SocketUserMessage userMessage))
            {
                return Task.CompletedTask;
            }

            int argPos = 0;
            if (!userMessage.HasStringPrefix("??", ref argPos))
            {
                return Task.CompletedTask;
            }

            _botService.ExecuteHandlerAsyncronously(
            handler: (client) =>
            {
                var context = new SocketCommandContext(client, userMessage);
                return _commandService.ExecuteAsync(context, argPos, _provider);
            },
            callback: async (result) =>
            {
                if(result.IsSuccess)
                {
                    return;
                }

                if(result.Error.HasValue)
                {
                    await message.Channel.SendMessageAsync($"Error: {result.Error.Value}, {result.ErrorReason}");
                }
            });
            return Task.CompletedTask;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _commandService.Log += LogCommand;
            await _commandService.AddModulesAsync(Assembly.GetEntryAssembly(), _provider);
            _botService.DiscordClient.MessageReceived += BotMessageReceived;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _botService.DiscordClient.MessageReceived -= BotMessageReceived;
            _botService.DiscordClient.Log -= LogCommand;
            return Task.CompletedTask;
        }
    }
}
