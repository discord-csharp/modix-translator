using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading.Tasks;

namespace ModixTranslator.HostedServices
{
    public interface IBotService : IHostedService, IAsyncDisposable
    {
        public DiscordSocketClient DiscordClient { get; }
        public void ExecuteHandlerAsyncronously<TReturn>(Func<DiscordSocketClient, Task<TReturn>> handler, Action<TReturn> callback);
    }
}
