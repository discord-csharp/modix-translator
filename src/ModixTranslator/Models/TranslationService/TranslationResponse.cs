using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ModixTranslator.Models.TranslationService
{
    public class TranslationResponse
    {
        [JsonPropertyName("translations")]
        public List<TranslatedText>? Translations { get; set; }

        [JsonPropertyName("detectedLanguage")]
        public LanguageData? DetectedLanguage { get; set; }
    }

    public class LanguageData
    {
        [JsonPropertyName("langauge")]
        public string Language { get; set; }

        [JsonPropertyName("score")]
        public float Score { get; set; }
    }
}
