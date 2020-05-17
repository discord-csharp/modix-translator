using Discord;

namespace ModixTranslator.Models.Translator
{
    public class ChannelPair
    {
        public ITextChannel? LangChannel { get; set; }
        public ITextChannel? EnglishChannel { get; set; }
    }
}
