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
    public class PayPalClient
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly string _baseApiUrl;
        private readonly string _clientId;
        private readonly string _clientSecret;

        private AuthResponse _authResponse;

        public PayPalClient(BillingSettings billingSettings)
        {
            _baseApiUrl = _baseApiUrl = !billingSettings.PayPal.Production ? "https://api.sandbox.paypal.com/{0}" :
                "https://api.paypal.com/{0}";
            _clientId = billingSettings.PayPal.ClientId;
            _clientSecret = billingSettings.PayPal.ClientSecret;
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

        public class Event<T>
        {
            [JsonProperty("id")]
            public string Id { get; set; }
            [JsonProperty("event_type")]
            public string EventType { get; set; }
            [JsonProperty("resource_type")]
            public string ResourceType { get; set; }
            [JsonProperty("create_time")]
            public DateTime CreateTime { get; set; }
            public T Resource { get; set; }
        }

        public class Refund : Sale
        {
            [JsonProperty("total_refunded_amount")]
            public ValueInfo TotalRefundedAmount { get; set; }
            [JsonProperty("sale_id")]
            public string SaleId { get; set; }
        }

        public class Sale
        {
            [JsonProperty("id")]
            public string Id { get; set; }
            [JsonProperty("state")]
            public string State { get; set; }
            [JsonProperty("amount")]
            public AmountInfo Amount { get; set; }
            [JsonProperty("parent_payment")]
            public string ParentPayment { get; set; }
            [JsonProperty("custom")]
            public string Custom { get; set; }
            [JsonProperty("create_time")]
            public DateTime CreateTime { get; set; }
            [JsonProperty("update_time")]
            public DateTime UpdateTime { get; set; }

            public Tuple<Guid?, Guid?> GetIdsFromCustom()
            {
                Guid? orgId = null;
                Guid? userId = null;

                if(!string.IsNullOrWhiteSpace(Custom) && Custom.Contains(":"))
                {
                    var parts = Custom.Split(':');
                    if(parts.Length > 1 && Guid.TryParse(parts[1], out var id))
                    {
                        if(parts[0] == "user_id")
                        {
                            userId = id;
                        }
                        else if(parts[0] == "organization_id")
                        {
                            orgId = id;
                        }
                    }
                }

                return new Tuple<Guid?, Guid?>(orgId, userId);
            }

            public bool GetCreditFromCustom()
            {
                return Custom.Contains("credit:true");
            }
        }

        public class AmountInfo
        {
            [JsonProperty("total")]
            public string Total { get; set; }
            public decimal TotalAmount => Convert.ToDecimal(Total);
        }

        public class ValueInfo
        {
            [JsonProperty("value")]
            public string Value { get; set; }
            public decimal ValueAmount => Convert.ToDecimal(Value);
        }
    }
}
