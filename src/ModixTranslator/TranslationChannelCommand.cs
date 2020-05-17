using Discord.Commands;
using System.Threading.Tasks;

namespace TranslatorBot9000
{
    [Group("translate")]
    public class TranslationChannelCommand : ModuleBase<SocketCommandContext>
    {
        private readonly ILocalizerHostedService _localizer;

        public TranslationChannelCommand(ILocalizerHostedService localizer)
        {
            _localizer = localizer;
        }

        [Command("create")]
        public async Task Create(string lang)
        {
            try
            {
                var pair = await _localizer.GetOrCreateChannelPair(Context.Guild, lang);

                if(pair?.EnglishChannel == null || pair?.LangChannel == null)
                {
                    await Context.Channel.SendMessageAsync("Unable to create channel pair");
                    return;
                }

                await Context.Channel.SendMessageAsync($"Translation channels have been created at {pair.EnglishChannel.Mention} and {pair.LangChannel.Mention}");
            }
            catch(LanguageNotSupportedException ex)
            {
                await Context.Channel.SendMessageAsync(ex.Message);
            }
        }
    }
}
