using System.Text.Json.Serialization;

namespace ModixTranslator.Models.TranslationService
{
    public partial class TranslationService
    {
        public class TranslationRequest
        {
            public TranslationRequest(string text)
            {
                Text = text;
            }

            [JsonPropertyName("text")]
            public string Text { get; private set; }
        }
    }
}
