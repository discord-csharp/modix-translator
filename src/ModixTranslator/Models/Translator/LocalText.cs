namespace ModixTranslator.Models.Translator
{
    public class LocalText
    {
        public LocalText(string language, string text)
        {
            Language = language;
            Text = text;
        }

        public string Language { get; set; }

        public string Text { get; set; }
    }
}