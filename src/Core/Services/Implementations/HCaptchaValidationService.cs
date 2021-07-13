using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Bit.Core.Services
{
    public class HCaptchaValidationService : ICaptchaValidationService
    {
        private readonly ILogger<HCaptchaValidationService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly GlobalSettings _globalSettings;

        public HCaptchaValidationService(
            ILogger<HCaptchaValidationService> logger,
            IHttpClientFactory httpClientFactory,
            GlobalSettings globalSettings)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _globalSettings = globalSettings;
        }

        public bool ServiceEnabled => true;
        public string SiteKey => _globalSettings.Captcha.HCaptchaSiteKey;

        public async Task<bool> ValidateCaptchaResponseAsync(string captchResponse, string clientIpAddress)
        {
            if (string.IsNullOrWhiteSpace(captchResponse))
            {
                return false;
            }

            var httpClient = _httpClientFactory.CreateClient("HCaptchaValidationService");

            var requestMessage = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri("https://hcaptcha.com/siteverify"),
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    { "response", captchResponse.TrimStart("hcaptcha|".ToCharArray()) },
                    { "secret", _globalSettings.Captcha.HCaptchaSecretKey },
                    { "sitekey", SiteKey },
                    { "remoteip", clientIpAddress }
                })
            };

            HttpResponseMessage responseMessage;
            try
            {
                responseMessage = await httpClient.SendAsync(requestMessage);
            }
            catch (Exception e)
            {
                _logger.LogError(11389, e, "Unable to verify with HCaptcha.");
                return false;
            }

            if (!responseMessage.IsSuccessStatusCode)
            {
                return false;
            }

            var responseContent = await responseMessage.Content.ReadAsStringAsync();
            dynamic jsonResponse = JsonConvert.DeserializeObject(responseContent);
            return (bool)jsonResponse.success;
        }
    }
}
