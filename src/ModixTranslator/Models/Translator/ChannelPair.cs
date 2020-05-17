using Discord;

namespace ModixTranslator.Models.Translator
{
    public class ChannelPair
    {
        public ITextChannel? TranslationChannel { get; set; }
        public ITextChannel? StandardLangChanel { get; set; }
    }
}
