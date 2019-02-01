using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Bit.Billing.Utilities
{
    public class PaypalClient
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly string _baseApiUrl;
        private readonly string _clientId;
        private readonly string _clientSecret;

        private AuthResponse _authResponse;

        public PaypalClient(BillingSettings billingSettings)
        {
            _baseApiUrl = _baseApiUrl = !billingSettings.Paypal.Production ? "https://api.sandbox.paypal.com/{0}" :
                "https://api.paypal.com/{0}";
            _clientId = billingSettings.Paypal.ClientId;
            _clientSecret = billingSettings.Paypal.ClientSecret;
        }

        public async Task<bool> VerifyWebhookAsync(string webhookJson, IHeaderDictionary headers, string webhookId)
        {
            if(webhookJson == null)
            {
                throw new ArgumentException("No webhook json.");
            }

            if(headers == null)
            {
                throw new ArgumentException("No headers.");
            }

            if(!headers.ContainsKey("PAYPAL-TRANSMISSION-ID"))
            {
                return false;
            }

            await AuthIfNeededAsync();

            var req = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(string.Format(_baseApiUrl, "v1/notifications/verify-webhook-signature"))
            };
            req.Headers.Authorization = new AuthenticationHeaderValue(
                _authResponse.TokenType, _authResponse.AccessToken);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var verifyRequest = new VerifyWebookRequest
            {
                AuthAlgo = headers["PAYPAL-AUTH-ALGO"],
                CertUrl = headers["PAYPAL-CERT-URL"],
                TransmissionId = headers["PAYPAL-TRANSMISSION-ID"],
                TransmissionTime = headers["PAYPAL-TRANSMISSION-TIME"],
                TransmissionSig = headers["PAYPAL-TRANSMISSION-SIG"],
                WebhookId = webhookId
            };
            var verifyRequestJson = JsonConvert.SerializeObject(verifyRequest);
            verifyRequestJson = verifyRequestJson.Replace("\"__WEBHOOK_BODY__\"", webhookJson);
            req.Content = new StringContent(verifyRequestJson, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(req);
            if(!response.IsSuccessStatusCode)
            {
                throw new Exception("Failed to verify webhook");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var verifyResponse = JsonConvert.DeserializeObject<VerifyWebookResponse>(responseContent);
            return verifyResponse.Verified;
        }

        private async Task<bool> AuthIfNeededAsync()
        {
            if(_authResponse?.Expired ?? true)
            {
                var req = new HttpRequestMessage
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri(string.Format(_baseApiUrl, "v1/oauth2/token"))
                };
                var authVal = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
                req.Headers.Authorization = new AuthenticationHeaderValue("Basic", authVal);
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                req.Content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "client_credentials")
                });

                var response = await _httpClient.SendAsync(req);
                if(!response.IsSuccessStatusCode)
                {
                    throw new Exception("Failed to auth with PayPal");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                _authResponse = JsonConvert.DeserializeObject<AuthResponse>(responseContent);
                return true;
            }
            return false;
        }

        public class VerifyWebookRequest
        {
            [JsonProperty("auth_algo")]
            public string AuthAlgo { get; set; }
            [JsonProperty("cert_url")]
            public string CertUrl { get; set; }
            [JsonProperty("transmission_id")]
            public string TransmissionId { get; set; }
            [JsonProperty("transmission_sig")]
            public string TransmissionSig { get; set; }
            [JsonProperty("transmission_time")]
            public string TransmissionTime { get; set; }
            [JsonProperty("webhook_event")]
            public string WebhookEvent { get; set; } = "__WEBHOOK_BODY__";
            [JsonProperty("webhook_id")]
            public string WebhookId { get; set; }
        }

        public class VerifyWebookResponse
        {
            [JsonProperty("verification_status")]
            public string VerificationStatus { get; set; }
            public bool Verified => VerificationStatus == "SUCCESS";
        }

        public class AuthResponse
        {
            private DateTime _created;

            public AuthResponse()
            {
                _created = DateTime.UtcNow;
            }

            [JsonProperty("scope")]
            public string Scope { get; set; }
            [JsonProperty("nonce")]
            public string Nonce { get; set; }
            [JsonProperty("access_token")]
            public string AccessToken { get; set; }
            [JsonProperty("token_type")]
            public string TokenType { get; set; }
            [JsonProperty("app_id")]
            public string AppId { get; set; }
            [JsonProperty("expires_in")]
            public long ExpiresIn { get; set; }
            public bool Expired => DateTime.UtcNow > _created.AddSeconds(ExpiresIn - 30);
        }
    }
}
