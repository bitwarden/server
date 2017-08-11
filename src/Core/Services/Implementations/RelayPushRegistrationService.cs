using System.Collections.Generic;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text;
using System;
using Newtonsoft.Json.Linq;
using Bit.Core.Utilities;
using System.Net;
using Bit.Core.Models.Api;
using Bit.Core.Enums;
using System.Linq;

namespace Bit.Core.Services
{
    public class RelayPushRegistrationService : IPushRegistrationService
    {
        private readonly HttpClient _pushClient;
        private readonly HttpClient _identityClient;
        private readonly GlobalSettings _globalSettings;
        private string _accessToken;
        private dynamic _decodedToken;
        private DateTime? _nextAuthAttempt = null;

        public RelayPushRegistrationService(
            GlobalSettings globalSettings)
        {
            _globalSettings = globalSettings;

            _pushClient = new HttpClient
            {
                BaseAddress = new Uri(globalSettings.PushRelayBaseUri)
            };

            _identityClient = new HttpClient
            {
                BaseAddress = new Uri(globalSettings.Installation.IdentityUri)
            };
        }

        public async Task CreateOrUpdateRegistrationAsync(string pushToken, string deviceId, string userId,
            string identifier, DeviceType type)
        {
            var tokenStateResponse = await HandleTokenStateAsync();
            if(!tokenStateResponse)
            {
                return;
            }

            var requestModel = new PushRegistrationRequestModel
            {
                DeviceId = deviceId,
                Identifier = identifier,
                PushToken = pushToken,
                Type = type,
                UserId = userId
            };

            var message = new TokenHttpRequestMessage(requestModel, _accessToken)
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(_pushClient.BaseAddress, "register")
            };
            await _pushClient.SendAsync(message);
        }

        public async Task DeleteRegistrationAsync(string deviceId)
        {
            var tokenStateResponse = await HandleTokenStateAsync();
            if(!tokenStateResponse)
            {
                return;
            }

            var message = new TokenHttpRequestMessage(_accessToken)
            {
                Method = HttpMethod.Delete,
                RequestUri = new Uri(_pushClient.BaseAddress, deviceId)
            };
            await _pushClient.SendAsync(message);
        }

        public async Task AddUserRegistrationOrganizationAsync(IEnumerable<string> deviceIds, string organizationId)
        {
            if(!deviceIds.Any())
            {
                return;
            }

            var tokenStateResponse = await HandleTokenStateAsync();
            if(!tokenStateResponse)
            {
                return;
            }

            var requestModel = new PushUpdateRequestModel(deviceIds, organizationId);
            var message = new TokenHttpRequestMessage(requestModel, _accessToken)
            {
                Method = HttpMethod.Put,
                RequestUri = new Uri(_pushClient.BaseAddress, "add-organization")
            };
            await _pushClient.SendAsync(message);
        }

        public async Task DeleteUserRegistrationOrganizationAsync(IEnumerable<string> deviceIds, string organizationId)
        {
            if(!deviceIds.Any())
            {
                return;
            }

            var tokenStateResponse = await HandleTokenStateAsync();
            if(!tokenStateResponse)
            {
                return;
            }

            var requestModel = new PushUpdateRequestModel(deviceIds, organizationId);
            var message = new TokenHttpRequestMessage(requestModel, _accessToken)
            {
                Method = HttpMethod.Put,
                RequestUri = new Uri(_pushClient.BaseAddress, "delete-organization")
            };
            await _pushClient.SendAsync(message);
        }

        private async Task<bool> HandleTokenStateAsync()
        {
            if(_nextAuthAttempt.HasValue && DateTime.UtcNow > _nextAuthAttempt.Value)
            {
                return false;
            }
            _nextAuthAttempt = null;

            if(!string.IsNullOrWhiteSpace(_accessToken) && !TokenExpired())
            {
                return true;
            }

            var requestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(_identityClient.BaseAddress, "connect/token"),
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "grant_type", "client_credentials" },
                    { "scope", "api.push" },
                    { "client_id", $"installation.{_globalSettings.Installation.Id}" },
                    { "client_secret", $"{_globalSettings.Installation.Key}" }
                })
            };

            var response = await _identityClient.SendAsync(requestMessage);
            if(!response.IsSuccessStatusCode)
            {
                if(response.StatusCode == HttpStatusCode.BadRequest)
                {
                    _nextAuthAttempt = DateTime.UtcNow.AddDays(1);
                }

                return false;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            dynamic tokenResponse = JsonConvert.DeserializeObject(responseContent);
            _accessToken = (string)tokenResponse.access_token;
            return true;
        }

        public class TokenHttpRequestMessage : HttpRequestMessage
        {
            public TokenHttpRequestMessage(string token)
            {
                Headers.Add("Authorization", $"Bearer {token}");
            }

            public TokenHttpRequestMessage(object requestObject, string token)
                : this(token)
            {
                var stringContent = JsonConvert.SerializeObject(requestObject);
                Content = new StringContent(stringContent, Encoding.UTF8, "application/json");
            }
        }

        public bool TokenExpired()
        {
            var decoded = DecodeToken();
            var exp = decoded?["exp"];
            if(exp == null)
            {
                throw new InvalidOperationException("No exp in token.");
            }

            var expiration = CoreHelpers.FromEpocMilliseconds(1000 * exp.Value<long>());
            return DateTime.UtcNow < expiration;
        }

        private JObject DecodeToken()
        {
            if(_decodedToken != null)
            {
                return _decodedToken;
            }

            if(_accessToken == null)
            {
                throw new InvalidOperationException($"{nameof(_accessToken)} not found.");
            }

            var parts = _accessToken.Split('.');
            if(parts.Length != 3)
            {
                throw new InvalidOperationException($"{nameof(_accessToken)} must have 3 parts");
            }

            var decodedBytes = Base64UrlDecode(parts[1]);
            if(decodedBytes == null || decodedBytes.Length < 1)
            {
                throw new InvalidOperationException($"{nameof(_accessToken)} must have 3 parts");
            }

            _decodedToken = JObject.Parse(Encoding.UTF8.GetString(decodedBytes, 0, decodedBytes.Length));
            return _decodedToken;
        }

        private byte[] Base64UrlDecode(string input)
        {
            var output = input;
            // 62nd char of encoding
            output = output.Replace('-', '+');
            // 63rd char of encoding
            output = output.Replace('_', '/');
            // Pad with trailing '='s
            switch(output.Length % 4)
            {
                case 0:
                    // No pad chars in this case
                    break;
                case 2:
                    // Two pad chars
                    output += "=="; break;
                case 3:
                    // One pad char
                    output += "="; break;
                default:
                    throw new InvalidOperationException("Illegal base64url string!");
            }

            // Standard base64 decoder
            return Convert.FromBase64String(output);
        }
    }
}
