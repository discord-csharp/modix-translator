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

        public Translation((string lang, string text) from, (string lang, string text) to)
        {
            _original = new LocalText(from.lang, from.text);
            Translated = new LocalText(to.lang, to.text);
        }

        public LocalText GuildLocal => Type == TranslationType.GuildLocale ? Translated : _original;

        public LocalText Foreign => Type == TranslationType.Foreign ? Translated : _original;

        public LocalText Translated { get; }

        public TranslationType Type { get; set; }
    }
}