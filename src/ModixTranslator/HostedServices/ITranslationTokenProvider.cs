using Microsoft.Extensions.Hosting;
using System;

namespace ModixTranslator.HostedServices
{
    public interface ITranslationTokenProvider : IHostedService, IAsyncDisposable
    {
        public string? Token { get; }
    }
}
