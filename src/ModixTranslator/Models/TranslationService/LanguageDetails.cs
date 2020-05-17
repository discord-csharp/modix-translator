using System.Text.Json.Serialization;

namespace ModixTranslator.Models.TranslationService
{
    public class LanguageDetails
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("nativeName")]
        public string? NativeName { get; set; }

        [JsonPropertyName("dir")]
        public string? Direction { get; set; }
    }
}
