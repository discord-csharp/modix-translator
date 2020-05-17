using System.Text.Json.Serialization;

namespace ModixTranslator.Models.TranslationService
{
    public class TranslatedText
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
