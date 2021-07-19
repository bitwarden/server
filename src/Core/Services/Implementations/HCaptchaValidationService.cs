using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Bit.Core.Services
{
    public class HCaptchaValidationService : ICaptchaValidationService
    {
        private const double TokenLifetimeInHours = (double)5 / 60; // 5 minutes
        private const string TokenName = "CaptchaBypassToken";
        private readonly ILogger<HCaptchaValidationService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly GlobalSettings _globalSettings;
        private readonly IDataProtector _dataProtector;

        public HCaptchaValidationService(
            ILogger<HCaptchaValidationService> logger,
            IHttpClientFactory httpClientFactory,
            IDataProtectionProvider dataProtectorProvider,
            GlobalSettings globalSettings)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _globalSettings = globalSettings;
            _dataProtector = dataProtectorProvider.CreateProtector("CaptchaServiceDataProtector");
        }

        public bool ServiceEnabled => true;
        public string SiteKey => _globalSettings.Captcha.HCaptchaSiteKey;
        public bool RequireCaptcha => _globalSettings.Captcha.RequireCaptcha;

        public string GenerateCaptchaBypassToken(User user) => _dataProtector.Protect(CaptchaBypassTokenContent(user));
        public bool ValidateCaptchaBypassToken(string encryptedToken, User user) =>
            user != null && CoreHelpers.TokenIsValid(TokenName, _dataProtector, encryptedToken, user.Email, user.Id,
            TokenLifetimeInHours);

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

        private static string CaptchaBypassTokenContent(User user) =>
            string.Join(' ', new object[] {
                TokenName,
                user.Id,
                user.Email,
                CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow.AddHours(TokenLifetimeInHours))
            });
    }
}
