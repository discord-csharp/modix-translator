using System.Text.Json.Serialization;

namespace ModixTranslator.Models.TranslationService
{
    public class DetectedLanguage
    {
        [JsonPropertyName("langauge")]
        public string Language { get; set; }

        [JsonPropertyName("score")]
        public float Score { get; set; }
    }
}