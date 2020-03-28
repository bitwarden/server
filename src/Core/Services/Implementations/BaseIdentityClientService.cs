using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text;
using System;
using Newtonsoft.Json.Linq;
using Bit.Core.Utilities;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services
{
    public abstract class BaseIdentityClientService
    {
        private readonly string _identityScope;
        private readonly string _identityClientId;
        private readonly string _identityClientSecret;
        private readonly ILogger<BaseIdentityClientService> _logger;

        private dynamic _decodedToken;
        private DateTime? _nextAuthAttempt = null;

        public BaseIdentityClientService(
            string baseClientServerUri,
            string baseIdentityServerUri,
            string identityScope,
            string identityClientId,
            string identityClientSecret,
            ILogger<BaseIdentityClientService> logger)
        {
            _identityScope = identityScope;
            _identityClientId = identityClientId;
            _identityClientSecret = identityClientSecret;
            _logger = logger;

            Client = new HttpClient
            {
                BaseAddress = new Uri(baseClientServerUri)
            };
            Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            IdentityClient = new HttpClient
            {
                BaseAddress = new Uri(baseIdentityServerUri)
            };
            IdentityClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        protected HttpClient Client { get; private set; }
        protected HttpClient IdentityClient { get; private set; }
        protected string AccessToken { get; private set; }

        protected async Task SendAsync(HttpMethod method, string path, object requestModel = null)
        {
            var tokenStateResponse = await HandleTokenStateAsync();
            if (!tokenStateResponse)
            {
                return;
            }

            var message = new TokenHttpRequestMessage(requestModel, AccessToken)
            {
                Method = method,
                RequestUri = new Uri(string.Concat(Client.BaseAddress, path))
            };

            try
            {
                var response = await Client.SendAsync(message);
            }
            catch (Exception e)
            {
                _logger.LogError(12334, e, "Failed to send to {0}.", message.RequestUri.ToString());
            }
        }

        protected async Task<bool> HandleTokenStateAsync()
        {
            if (_nextAuthAttempt.HasValue && DateTime.UtcNow > _nextAuthAttempt.Value)
            {
                return false;
            }
            _nextAuthAttempt = null;

            if (!string.IsNullOrWhiteSpace(AccessToken) && !TokenNeedsRefresh())
            {
                return true;
            }

            var requestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(string.Concat(IdentityClient.BaseAddress, "connect/token")),
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "grant_type", "client_credentials" },
                    { "scope", _identityScope },
                    { "client_id", _identityClientId },
                    { "client_secret", _identityClientSecret }
                })
            };

            HttpResponseMessage response = null;
            try
            {
                response = await IdentityClient.SendAsync(requestMessage);
            }
            catch (Exception e)
            {
                _logger.LogError(12339, e, "Unable to authenticate with identity server.");
            }

            if (response == null)
            {
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    _nextAuthAttempt = DateTime.UtcNow.AddDays(1);
                }

                return false;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            dynamic tokenResponse = JsonConvert.DeserializeObject(responseContent);
            AccessToken = (string)tokenResponse.access_token;
            return true;
        }

        protected class TokenHttpRequestMessage : HttpRequestMessage
        {
            public TokenHttpRequestMessage(string token)
            {
                Headers.Add("Authorization", $"Bearer {token}");
            }

            public TokenHttpRequestMessage(object requestObject, string token)
                : this(token)
            {
                if (requestObject != null)
                {
                    var stringContent = JsonConvert.SerializeObject(requestObject);
                    Content = new StringContent(stringContent, Encoding.UTF8, "application/json");
                }
            }
        }

        protected bool TokenNeedsRefresh(int minutes = 5)
        {
            var decoded = DecodeToken();
            var exp = decoded?["exp"];
            if (exp == null)
            {
                throw new InvalidOperationException("No exp in token.");
            }

            var expiration = CoreHelpers.FromEpocSeconds(exp.Value<long>());
            return DateTime.UtcNow.AddMinutes(-1 * minutes) > expiration;
        }

        protected JObject DecodeToken()
        {
            if (_decodedToken != null)
            {
                return _decodedToken;
            }

            if (AccessToken == null)
            {
                throw new InvalidOperationException($"{nameof(AccessToken)} not found.");
            }

            var parts = AccessToken.Split('.');
            if (parts.Length != 3)
            {
                throw new InvalidOperationException($"{nameof(AccessToken)} must have 3 parts");
            }

            var decodedBytes = CoreHelpers.Base64UrlDecode(parts[1]);
            if (decodedBytes == null || decodedBytes.Length < 1)
            {
                throw new InvalidOperationException($"{nameof(AccessToken)} must have 3 parts");
            }

            _decodedToken = JObject.Parse(Encoding.UTF8.GetString(decodedBytes, 0, decodedBytes.Length));
            return _decodedToken;
        }
    }
}
