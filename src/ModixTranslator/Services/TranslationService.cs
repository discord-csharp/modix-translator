using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ModixTranslator.HostedServices;
using ModixTranslator.Models.TranslationService;
using ModixTranslator.Models.Translator;

namespace ModixTranslator.Behaviors
{
    public class TranslationService : ITranslationService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TranslationService> _logger;
        private readonly ITranslationTokenProvider _tokenProvider;

        //                                     matches `text` or <mention/emoji>
        private readonly Regex inlinePattern = new Regex(@"`.*?`|<(@[!&]|#|a?:.+?:)[0-9]{17,19}>",
            RegexOptions.Singleline | RegexOptions.Compiled);

        private readonly Regex keyScrubber = new Regex(@"\D", RegexOptions.Compiled);

        private readonly JsonSerializerOptions options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public TranslationService(ILogger<TranslationService> logger, IHttpClientFactory httpClientFactory,
            ITranslationTokenProvider tokenProvider)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _tokenProvider = tokenProvider;
        }

        public static Regex CodeBlockPattern { get; } =
            new Regex("```.*?```", RegexOptions.Singleline | RegexOptions.Compiled);

        public async Task<bool> IsLangSupported(string lang)
        {
            var response = await GetSupportedLanguages();
            return response.Translation.ContainsKey(lang);
        }

        public async Task<SupportedLanguageResponse> GetSupportedLanguages()
        {
            using var client = _httpClientFactory.CreateClient();
            var supportedResponse =
                await client.GetAsync(
                    "https://api.cognitive.microsofttranslator.com/languages?api-version=3.0&scope=translation");
            if (!supportedResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Unable to determine if a language is supported");
                return new SupportedLanguageResponse();
            }

            var responseText = await supportedResponse.Content!.ReadAsStringAsync();

            var response = JsonSerializer.Deserialize<SupportedLanguageResponse>(responseText, options)!;
            return response!;
        }

        public async Task<Translation> GetTranslation(string? from, string to, string text)
        {
            _logger.LogDebug($"Translating {text} from {from ?? "auto"} to {to}");

            string translated;

            var strippedText = text;
            var codeBlocks = StripText(ref strippedText, CodeBlockPattern);
            var inlined = StripText(ref strippedText, inlinePattern);
            try
            {
                var client = _httpClientFactory.CreateClient("translationClient");
                var token = _tokenProvider.Token;
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                client.BaseAddress = new Uri("https://api.cognitive.microsofttranslator.com/");

                var url = $"translate?api-version=3.0&to={to}";

                if (!string.IsNullOrWhiteSpace(from)) url += $"&from={from}";

                var requestBody = JsonSerializer.Serialize(new[] { new TranslationRequest(strippedText) }, options);
                var response = await client.PostAsync(url,
                    new StringContent(requestBody, Encoding.UTF8, "application/json"));

                if (!response.IsSuccessStatusCode)
                {
                    translated = text;
                    var body = await response.Content!.ReadAsStringAsync();
                    throw new InvalidOperationException(
                        $"Unable to translate. Service returned {response.StatusCode}: {body}");
                }

                var json = await response.Content!.ReadAsStringAsync();
                var translationResponse = JsonSerializer.Deserialize<List<TranslationResponse>>(json, options);
                translationResponse = translationResponse ??
                                      throw new InvalidOperationException("Response is not a valid translation");

                if (translationResponse.Count == 0)
                    throw new InvalidOperationException("No translations were returned");

                var translation = translationResponse.FirstOrDefault()?.Translations?.FirstOrDefault();

                if (translation is null) throw new InvalidOperationException("No translations were returned");

                // Use the auto-detected language if it's present
                from ??= translationResponse.FirstOrDefault()?.DetectedLanguage?.Language;
                translated = UnstripText(translation.Text ?? strippedText, inlined, codeBlocks);
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, $"Unable to translate message: {ex.Message}");
                translated = text;
            }

            _logger.LogDebug("Finished translating");
            return new Translation((from ?? "unknown", text), (to, translated));
        }

        private Dictionary<string, string> StripText(ref string text, Regex pattern)
        {
            var replacements = new Dictionary<string, string>();

            text = pattern.Replace(text, str =>
            {
                var guid = $"{{{keyScrubber.Replace($"{Guid.NewGuid()}", string.Empty)}}}";
                _logger.LogDebug($"Replacing {str} with {guid}");
                replacements[guid] = str.Value;
                return guid;
            });
            return replacements;
        }

        private string UnstripText(string text, params Dictionary<string, string>[] replacements)
        {
            var sb = new StringBuilder(text);
            foreach (var replacement in replacements)
            foreach (var r in replacement)
                sb.Replace(r.Key, r.Value);
            return sb.ToString();
        }
    }
}