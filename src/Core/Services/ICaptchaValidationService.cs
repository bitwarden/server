using System.Threading.Tasks;
using Bit.Core.Context;
using Bit.Core.Entities;

namespace Bit.Core.Services
{
    public interface ICaptchaValidationService
    {
        string SiteKey { get; }
        string SiteKeyResponseKeyName { get; }
        bool RequireCaptchaValidation(ICurrentContext currentContext, int failedLoginCount = 0);
        Task<bool> ValidateCaptchaResponseAsync(string captchResponse, string clientIpAddress);
        string GenerateCaptchaBypassToken(User user);
        bool ValidateCaptchaBypassToken(string encryptedToken, User user);
        bool ValidateFailedAuthEmailConditions(bool unknownDevice, int failedLoginCount);
    }
}
