namespace ModixTranslator.Models.Translator
{
    public class Translation
    {
        public Translation(string text, string language)
        {
            Text = text;
            Language = language;
        }

        public string Text { get; set; }

        public string Language { get; set; }
    }
}