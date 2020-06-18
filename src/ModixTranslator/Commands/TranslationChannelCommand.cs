using Discord.Commands;
using ModixTranslator.Behaviors;
using ModixTranslator.HostedServices;
using ModixTranslator.Models.Translator;
using System;
using System.Threading.Tasks;

namespace ModixTranslator.Commands
{
    public class TranslationChannelCommand : ModuleBase<SocketCommandContext>
    {
        private readonly ITranslatorHostedService _localizer;
        private readonly ITranslationService _translator;

        public TranslationChannelCommand(ITranslatorHostedService localizer, ITranslationService translator)
        {
            _localizer = localizer;
            _translator = translator;
        }

        [Command("translate create"), Priority(1000)]
        public async Task Create(string lang)
        {
            try
            {
                var pair = await _localizer.GetOrCreateChannelPair(Context.Guild, lang);

                if(pair?.StandardLangChanel == null || pair?.TranslationChannel == null)
                {
                    await Context.Channel.SendMessageAsync("Unable to create channel pair");
                    return;
                }

                await Context.Channel.SendMessageAsync($"Translation channels have been created at {pair.StandardLangChanel.Mention} and {pair.TranslationChannel.Mention}");
            }
            catch(LanguageNotSupportedException ex)
            {
                await Context.Channel.SendMessageAsync(ex.Message);
            }
        }

        [Command("translate"), Priority(1)]
        public async Task Translate(string to, [Remainder]string text)
        {
            if(string.IsNullOrWhiteSpace(to))
            {
                throw new ArgumentNullException(nameof(to));
            }
            if(string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentNullException(nameof(text));
            }

            var translation = await _translator.GetTranslation(null, to, text);
            await Context.Channel.SendMessageAsync(translation.Translated.Text);
        }
    }
}
