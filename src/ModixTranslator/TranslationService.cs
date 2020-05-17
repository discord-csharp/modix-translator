using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TranslatorBot9000
{
    public interface ITranslationService
    {
        Task<string> GetTranslation(string from, string to, string text);
        Task<bool> IsLangSupported(string lang);
    }
    public class TranslationService : ITranslationService
    {
        private readonly ILogger<TranslationService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ITranslationTokenProvider _tokenProvider;

        private readonly JsonSerializerOptions options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public TranslationService(ILogger<TranslationService> logger, IHttpClientFactory httpClientFactory, ITranslationTokenProvider tokenProvider)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _tokenProvider = tokenProvider;
        }

        public async Task<bool> IsLangSupported(string lang)
        {
            using var client = _httpClientFactory.CreateClient();
            var supportedResponse = await client.GetAsync("https://api.cognitive.microsofttranslator.com/languages?api-version=3.0&scope=translation");
            if (!supportedResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Unable to determine if a language is supported");
                return false;
            }
            var responseText = await supportedResponse.Content!.ReadAsStringAsync();

            var response = JsonSerializer.Deserialize<SupportedLanguageResponse>(responseText, options)!;

            return response.Translation.ContainsKey(lang);
        }

        public async Task<string> GetTranslation(string from, string to, string text)
        {
            _logger.LogDebug($"Translating {text} from {from} to {to}");
            string message;
            try
            {
                var client = _httpClientFactory.CreateClient("translationClient");
                var token = _tokenProvider.Token;
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                client.BaseAddress = new Uri("https://api.cognitive.microsofttranslator.com/");

                var response = await client.PostAsJsonAsync($"translate?api-version=3.0&to={to}&from={from}", new[] { new TranslationRequest(text) }, options);

                if (!response.IsSuccessStatusCode)
                {
                    message = text;
                    var body = await response.Content!.ReadAsStringAsync();
                    throw new InvalidOperationException($"Unable to translate. Service returned {response.StatusCode}: {body}");
                }
                var json = await response.Content!.ReadAsStringAsync();
                var translationResponse = JsonSerializer.Deserialize<List<TranslationResponse>>(json, options);
                translationResponse = translationResponse ?? throw new InvalidOperationException("Response is not a valid translation");

                if (translationResponse.Count == 0)
                {
                    throw new InvalidOperationException("No translations were returned");
                }

                var translation = translationResponse.FirstOrDefault()?.Translations?.FirstOrDefault();

                if (translation is null)
                {
                    throw new InvalidOperationException("No translations were returned");
                }

                message = translation.Text ?? string.Empty;

            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, $"Unable to translate message: {ex.Message}");
                message = text;
            }

            _logger.LogDebug("Finished translating");
            return message;
        }

        public class TranslationRequest
        {
            public TranslationRequest(string text)
            {
                Text = text;
            }

            [JsonPropertyName("text")]
            public string Text { get; private set; }
        }

        public class TranslationResponse
        {
            [JsonPropertyName("translations")]
            public List<TranslatedText>? Translations { get; set; }

        }

        public class TranslatedText
        {
            [JsonPropertyName("text")]
            public string? Text { get; set; }
        }
        public class SupportedLanguageResponse
        {
            [JsonPropertyName("translation")]
            public Dictionary<string, LanguageDetails> Translation { get; set; } = new Dictionary<string, LanguageDetails>(StringComparer.OrdinalIgnoreCase);
        }
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
}
