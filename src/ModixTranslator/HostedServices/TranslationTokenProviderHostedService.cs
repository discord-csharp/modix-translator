using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ModixTranslator.HostedServices
{

    class TranslationTokenProviderHostedService : ITranslationTokenProvider
    {
        private readonly Timer _tokenUpdateTimer;
        private readonly IConfiguration _config;
        private readonly ILogger<TranslationTokenProviderHostedService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        private const string _authnEndpoint = "https://api.cognitive.microsoft.com/sts/v1.0/issueToken";
        private const string _keyHeaderName = "Ocp-Apim-Subscription-Key";
        private const string _configKeyName = "AzureTranslationKey";
        private const string _httpClientName = "authnClient";

        public string? Token { get; set; }

        public TranslationTokenProviderHostedService(IConfiguration config, ILogger<TranslationTokenProviderHostedService> logger, IHttpClientFactory httpClientFactory)
        {
            _config = config;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _tokenUpdateTimer = new Timer(async a => await GetToken());
        }

        private async Task GetToken()
        {
            _logger.LogDebug("Starting azure translation service token update");
            string key = _config[_configKeyName];
            using var authnHttpClient = _httpClientFactory.CreateClient(_httpClientName);
            try
            {
                authnHttpClient.DefaultRequestHeaders.TryAddWithoutValidation(_keyHeaderName, key);
                authnHttpClient.BaseAddress = new Uri(_authnEndpoint);

                var keyRequest = await authnHttpClient.PostAsync("", new StringContent(""));
                var token = await keyRequest.Content!.ReadAsStringAsync();

                Token = token;
            }
            catch(Exception ex)
            {
                _logger.LogCritical(ex, "Token update failed");
            }

            _logger.LogDebug("Finished azure translation service token update");
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _tokenUpdateTimer.Change(TimeSpan.Zero, TimeSpan.FromMinutes(8));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _tokenUpdateTimer.Change(Timeout.Infinite, Timeout.Infinite);
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            await _tokenUpdateTimer.DisposeAsync();
        }
    }
}
