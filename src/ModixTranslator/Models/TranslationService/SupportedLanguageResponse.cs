using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ModixTranslator.Models.TranslationService
{
    public partial class TranslationService
    {
        public class SupportedLanguageResponse
        {
            [JsonPropertyName("translation")]
            public Dictionary<string, LanguageDetails> Translation { get; set; } = new Dictionary<string, LanguageDetails>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
