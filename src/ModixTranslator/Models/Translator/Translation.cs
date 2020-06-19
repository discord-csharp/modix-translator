using System.Collections.Generic;
using System.Linq;

namespace ModixTranslator.Models.Translator
{
    public class Translation
    {
        public enum TranslationType
        {
            GuildLocale,
            Foreign
        }

        private readonly LocalText _original;

        public Translation((string lang, string text) from, (string lang, string text) to,
            IEnumerable<string>? codeBlocks = null)
        {
            _original = new LocalText(from.lang, from.text);
            Translated = new LocalText(to.lang, to.text);
            CodeBlocks = codeBlocks ?? Enumerable.Empty<string>();
        }

        public LocalText GuildLocal => Type == TranslationType.GuildLocale ? Translated : _original;

        public LocalText Foreign => Type == TranslationType.Foreign ? Translated : _original;

        public LocalText Translated { get; }

        public TranslationType Type { get; set; }

        public IEnumerable<string> CodeBlocks { get; set; }
    }
}