using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ModixTranslator.Models.TranslationService
{
    public class TranslationResponse
    {
        [JsonPropertyName("translations")]
        public List<TranslatedText>? Translations { get; set; }

    }
}
