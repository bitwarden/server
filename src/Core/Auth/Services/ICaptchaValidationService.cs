using Bit.Core.Auth.Models.Business;
using Bit.Core.Context;
using Bit.Core.Entities;

namespace Bit.Core.Auth.Services;

public interface ICaptchaValidationService
{
    string SiteKey { get; }
    string SiteKeyResponseKeyName { get; }
    bool RequireCaptchaValidation(ICurrentContext currentContext, User user = null);
    Task<CaptchaResponse> ValidateCaptchaResponseAsync(string captchaResponse, string clientIpAddress,
        User user = null);
    string GenerateCaptchaBypassToken(User user);
}
