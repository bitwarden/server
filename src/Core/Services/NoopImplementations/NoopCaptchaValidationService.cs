using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Models.Business;

namespace Bit.Core.Services;

public class NoopCaptchaValidationService : ICaptchaValidationService
{
    public string SiteKeyResponseKeyName => null;
    public string SiteKey => null;
    public bool RequireCaptchaValidation(ICurrentContext currentContext, User user = null) => false;
    public string GenerateCaptchaBypassToken(User user) => "";
    public Task<CaptchaResponse> ValidateCaptchaResponseAsync(string captchaResponse, string clientIpAddress,
        User user = null)
    {
        return Task.FromResult(new CaptchaResponse { Success = true });
    }
}
