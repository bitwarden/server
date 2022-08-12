using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#nullable enable

namespace Bit.Core.Utilities
{
    public class InstallationAuthenticatingHandler : DelegatingHandler
    {
        public static readonly HttpRequestOptionsKey<bool> OptOutKey = new("Bitwarden-OptOut");

        private readonly HttpClient _identityClient;
        private readonly ILogger<InstallationAuthenticatingHandler> _logger;
        private readonly IOptionsMonitor<ConnectTokenOptions> _options;
        private readonly string _optionsName;

        private ConnectTokenOptions _tokenOptions;
        private string? _accessToken;
        private DateTime? _nextAuthAttempt;

        public InstallationAuthenticatingHandler(HttpClient httpClient,
            ILogger<InstallationAuthenticatingHandler> logger,
            IOptionsMonitor<ConnectTokenOptions> options,
            string optionsName)
        {
            _identityClient = httpClient;
            _logger = logger;
            _options = options;
            _optionsName = optionsName;
            _tokenOptions = _options.Get(_optionsName);
            _options.OnChange((options, name) =>
            {
                if (name == _optionsName)
                {
                    _tokenOptions = options;
                }
            });
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (!request.Options.TryGetValue(OptOutKey, out var shouldOptOut) || !shouldOptOut)
            {
                // Attempt to add an authorization header since they don't have one
                if (!await EnsureTokenAsync(cancellationToken))
                {
                    throw new HttpRequestException($"Cannot obtain an access_token from {_identityClient.BaseAddress}, please check logs to determine the error.");
                }

                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            }

            return await base.SendAsync(request, cancellationToken);
        }

        private async Task<bool> EnsureTokenAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (_accessToken == null || DateTime.UtcNow > _nextAuthAttempt)
                {
                    var response = await CallConnectTokenAsync();
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        return false;
                    }
                    using var jsonDocument = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: cancellationToken);
                    (_accessToken, _nextAuthAttempt) = ReadTokenBody(jsonDocument!);
                }

                return true;
            }
            catch (InvalidOperationException invalidOperationException)
            {
                _logger.LogError(invalidOperationException, "Bad stuff.");
                return false;
            }


            static (string? accessToken, DateTime nextAuthAttempt) ReadTokenBody(JsonDocument jsonDocument)
            {
                var now = DateTime.UtcNow;
                var root = jsonDocument.RootElement;
                if (!root.TryGetProperty("access_token", out var accessTokenProperty))
                {
                    throw new InvalidOperationException("The response body did not contain an 'access_token' property");
                }

                if (!root.TryGetProperty("expires_in", out var expiresInProperty))
                {
                    throw new InvalidOperationException("The response body did not contain an 'expires_in' property");
                }


                var expiresIn = TimeSpan.FromSeconds(expiresInProperty.GetInt32())
                    .Subtract(TimeSpan.FromMinutes(10));


                return (accessTokenProperty.GetString(), now + expiresIn);
            }
        }

        private async Task<HttpResponseMessage> CallConnectTokenAsync()
        {
            return await _identityClient.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "client_id", _tokenOptions.ClientId },
                { "client_secret", _tokenOptions.ClientSecret },
                { "scope", _tokenOptions.Scope },
            }));
        }
    }
}
