using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Models.Business.Tokenables;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services
{
    public class HCaptchaValidationService : ICaptchaValidationService
    {
        private readonly ILogger<HCaptchaValidationService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly GlobalSettings _globalSettings;
        private readonly IDataProtectorTokenFactory<HCaptchaTokenable> _tokenizer;

        public HCaptchaValidationService(
            ILogger<HCaptchaValidationService> logger,
            IHttpClientFactory httpClientFactory,
            IDataProtectorTokenFactory<HCaptchaTokenable> tokenizer,
            GlobalSettings globalSettings)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _globalSettings = globalSettings;
            _tokenizer = tokenizer;
        }

        public string SiteKeyResponseKeyName => "HCaptcha_SiteKey";
        public string SiteKey => _globalSettings.Captcha.HCaptchaSiteKey;

        public string GenerateCaptchaBypassToken(User user) => _tokenizer.Protect(new HCaptchaTokenable(user));

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

            using var jsonDocument = await responseMessage.Content.ReadFromJsonAsync<JsonDocument>();
            var root = jsonDocument.RootElement;
            return root.GetProperty("success").GetBoolean();
        }

        public bool RequireCaptchaValidation(ICurrentContext currentContext, int failedLoginCount = 0)
        {
            var failedLoginCeiling = _globalSettings.Captcha.MaximumFailedLoginAttempts;
            return currentContext.IsBot ||
                   _globalSettings.Captcha.ForceCaptchaRequired ||
                   failedLoginCeiling > 0 && failedLoginCount >= failedLoginCeiling;
        }

        public bool ValidateFailedAuthEmailConditions(bool unknownDevice, int failedLoginCount)
        {
            var failedLoginCeiling = _globalSettings.Captcha.MaximumFailedLoginAttempts;
            return unknownDevice && failedLoginCeiling > 0 && failedLoginCount == failedLoginCeiling;
        }

        private static bool TokenIsApiKey(string bypassToken, User user) =>
            !string.IsNullOrWhiteSpace(bypassToken) && user != null && user.ApiKey == bypassToken;
        private bool TokenIsCaptchaBypassToken(string encryptedToken, User user)
        {
            return _tokenizer.TryUnprotect(encryptedToken, out var data) &&
                data.Valid && data.TokenIsValid(user);
        }
    }
}
