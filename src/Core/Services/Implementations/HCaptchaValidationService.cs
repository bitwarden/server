using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Bit.Core.Context;
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
        private const string TokenClearTextPrefix = "BWCaptchaBypass_";
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

        public string SiteKeyResponseKeyName => "HCaptcha_SiteKey";
        public string SiteKey => _globalSettings.Captcha.HCaptchaSiteKey;

        public string GenerateCaptchaBypassToken(User user) =>
            $"{TokenClearTextPrefix}{_dataProtector.Protect(CaptchaBypassTokenContent(user))}";

        public bool ValidateCaptchaBypassToken(string bypassToken, User user) =>
            TokenIsApiKey(bypassToken, user) || TokenIsCaptchaBypassToken(bypassToken, user);

        public async Task<bool> ValidateCaptchaResponseAsync(string captchaResponse, string clientIpAddress)
        {
            if (string.IsNullOrWhiteSpace(captchaResponse))
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
                    { "response", captchaResponse.TrimStart("hcaptcha|".ToCharArray()) },
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

        public bool RequireCaptchaValidation(ICurrentContext currentContext) =>
            currentContext.IsBot || _globalSettings.Captcha.ForceCaptchaRequired;

        private static string CaptchaBypassTokenContent(User user) =>
            string.Join(' ', new object[] {
                TokenName,
                user?.Id,
                user?.Email,
                CoreHelpers.ToEpocMilliseconds(DateTime.UtcNow.AddHours(TokenLifetimeInHours))
            });

        private static bool TokenIsApiKey(string bypassToken, User user) =>
            !string.IsNullOrWhiteSpace(bypassToken) && user != null && user.ApiKey == bypassToken;
        private bool TokenIsCaptchaBypassToken(string encryptedToken, User user) =>
            encryptedToken.StartsWith(TokenClearTextPrefix) && user != null &&
            CoreHelpers.TokenIsValid(TokenName, _dataProtector, encryptedToken[TokenClearTextPrefix.Length..],
            user.Email, user.Id, TokenLifetimeInHours);

    }
}
