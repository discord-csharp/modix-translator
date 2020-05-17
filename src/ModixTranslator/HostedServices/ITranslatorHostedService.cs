using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using ModixTranslator.Models.Translator;
using System.Threading.Tasks;

namespace ModixTranslator.HostedServices
{
    public interface ITranslatorHostedService : IHostedService
    {
        Task<ChannelPair?> GetOrCreateChannelPair(SocketGuild guild, string lang);
    }
}
