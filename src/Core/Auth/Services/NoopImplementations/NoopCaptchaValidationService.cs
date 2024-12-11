using Bit.Core.Auth.Models.Business;
using Bit.Core.Context;
using Bit.Core.Entities;

namespace Bit.Core.Auth.Services;

public class NoopCaptchaValidationService : ICaptchaValidationService
{
    public string SiteKeyResponseKeyName => null;
    public string SiteKey => null;

    public bool RequireCaptchaValidation(ICurrentContext currentContext, User user = null) => false;

    public string GenerateCaptchaBypassToken(User user) => "";

    public Task<CaptchaResponse> ValidateCaptchaResponseAsync(
        string captchaResponse,
        string clientIpAddress,
        User user = null
    )
    {
        return Task.FromResult(new CaptchaResponse { Success = true });
    }
}
