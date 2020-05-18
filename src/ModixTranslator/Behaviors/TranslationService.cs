using Microsoft.Extensions.Logging;
using ModixTranslator.HostedServices;
using ModixTranslator.Models.TranslationService;
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

namespace ModixTranslator.Behaviors
{
    public class TranslationService : ITranslationService
    {
        private readonly ILogger<TranslationService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ITranslationTokenProvider _tokenProvider;

        private readonly JsonSerializerOptions options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private readonly Regex pattern = new Regex("`{1,3}.*?`{1,3}|<.*?>", RegexOptions.Singleline | RegexOptions.Compiled);
        private readonly Regex keyScrubber = new Regex("\\D", RegexOptions.Compiled);

        public TranslationService(ILogger<TranslationService> logger, IHttpClientFactory httpClientFactory, ITranslationTokenProvider tokenProvider)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _tokenProvider = tokenProvider;
        }

        public async Task<bool> IsLangSupported(string lang)
        {
            var response = await GetSupportedLanguages();
            return response.Translation.ContainsKey(lang);
        }

        public async Task<SupportedLanguageResponse> GetSupportedLanguages()
        {
            using var client = _httpClientFactory.CreateClient();
            var supportedResponse = await client.GetAsync("https://api.cognitive.microsofttranslator.com/languages?api-version=3.0&scope=translation");
            if (!supportedResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Unable to determine if a language is supported");
                return new SupportedLanguageResponse();
            }
            var responseText = await supportedResponse.Content!.ReadAsStringAsync();

            var response = JsonSerializer.Deserialize<SupportedLanguageResponse>(responseText, options)!;
            return response!;
        }

    public async Task<string> GetTranslation(string? from, string to, string text)
    {
        _logger.LogDebug($"Translating {text} from {from ?? "auto"} to {to}");
        string message;
        try
        {
            var (strippedText, codeBlocks) = StripBlocksFromText(text);
            var client = _httpClientFactory.CreateClient("translationClient");
            var token = _tokenProvider.Token;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.BaseAddress = new Uri("https://api.cognitive.microsofttranslator.com/");

            var url = $"translate?api-version=3.0&to={to}";

            if (!string.IsNullOrWhiteSpace(from))
            {
                url += $"&from={from}";
            }

            var requestBody = JsonSerializer.Serialize(new[] { new TranslationRequest(strippedText) }, options);
            var response = await client.PostAsync(url, new StringContent(requestBody, Encoding.UTF8, "application/json"));

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

            message = AddCodeblocksToString(translation.Text ?? strippedText, codeBlocks);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, $"Unable to translate message: {ex.Message}");
            message = text;
        }

        _logger.LogDebug("Finished translating");
        return message;
    }

    private (string strippedText, Dictionary<string, string> codeBlocks) StripBlocksFromText(string text)
    {
        var replacements = new Dictionary<string, string>();

        var replacedText = pattern.Replace(text, str =>
        {
            var guid = $"{{{keyScrubber.Replace($"{Guid.NewGuid()}", string.Empty)}}}";
            _logger.LogDebug($"Replacing {str} with {guid}");
            replacements[guid] = str.Value;
            return guid;
        });
        return (replacedText, replacements);
    }

    private string AddCodeblocksToString(string text, Dictionary<string, string> replacements)
    {
        var sb = new StringBuilder(text);
        foreach (var replacement in replacements)
        {
            sb.Replace(replacement.Key, replacement.Value);
        }
        return sb.ToString();
    }
}
}
