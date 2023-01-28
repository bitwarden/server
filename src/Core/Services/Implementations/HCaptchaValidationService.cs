using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Models.Business;
using Bit.Core.Models.Business.Tokenables;
using Bit.Core.Settings;
using Bit.Core.Tokens;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Services;

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

    public async Task<CaptchaResponse> ValidateCaptchaResponseAsync(string captchaResponse, string clientIpAddress,
        User user = null)
    {
        var response = new CaptchaResponse { Success = false };
        if (string.IsNullOrWhiteSpace(captchaResponse))
        {
            return response;
        }

        if (user != null && ValidateCaptchaBypassToken(captchaResponse, user))
        {
            response.Success = true;
            return response;
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
            return response;
        }

        if (!responseMessage.IsSuccessStatusCode)
        {
            return response;
        }

        using var hcaptchaResponse = await responseMessage.Content.ReadFromJsonAsync<HCaptchaResponse>();
        response.Success = hcaptchaResponse.Success;
        var score = hcaptchaResponse.Score.GetValueOrDefault();
        response.MaybeBot = score >= _globalSettings.Captcha.MaybeBotScoreThreshold;
        response.IsBot = score >= _globalSettings.Captcha.IsBotScoreThreshold;
        response.Score = score;
        return response;
    }

    public bool RequireCaptchaValidation(ICurrentContext currentContext, User user = null)
    {
        if (user == null)
        {
            return currentContext.IsBot || _globalSettings.Captcha.ForceCaptchaRequired;
        }

        var failedLoginCeiling = _globalSettings.Captcha.MaximumFailedLoginAttempts;
        var failedLoginCount = user?.FailedLoginCount ?? 0;
        var cloudEmailUnverified = !_globalSettings.SelfHosted && !user.EmailVerified;
        return currentContext.IsBot ||
               _globalSettings.Captcha.ForceCaptchaRequired ||
               cloudEmailUnverified ||
               failedLoginCeiling > 0 && failedLoginCount >= failedLoginCeiling;
    }

    private static bool TokenIsValidApiKey(string bypassToken, User user) =>
        !string.IsNullOrWhiteSpace(bypassToken) && user != null && user.ApiKey == bypassToken;

    private bool TokenIsValidCaptchaBypassToken(string encryptedToken, User user)
    {
        return _tokenizer.TryUnprotect(encryptedToken, out var data) &&
            data.Valid && data.TokenIsValid(user);
    }

    private bool ValidateCaptchaBypassToken(string bypassToken, User user) =>
        TokenIsValidApiKey(bypassToken, user) || TokenIsValidCaptchaBypassToken(bypassToken, user);

    public class HCaptchaResponse : IDisposable
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }
        [JsonPropertyName("score")]
        public double? Score { get; set; }
        [JsonPropertyName("score_reason")]
        public List<string> ScoreReason { get; set; }

        public void Dispose() { }
    }
}
