using Discord;

namespace TranslatorBot9000
{
    public class ChannelPair
    {
        public ITextChannel? LangChannel { get; set; }
        public ITextChannel? EnglishChannel { get; set; }
    }
}
