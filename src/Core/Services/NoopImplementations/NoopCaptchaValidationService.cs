using System.Threading.Tasks;
using Bit.Core.Context;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public class NoopCaptchaValidationService : ICaptchaValidationService
    {
        public string SiteKeyResponseKeyName => null;
        public string SiteKey => null;
        public bool RequireCaptchaValidation(ICurrentContext currentContext) => false;

        public string GenerateCaptchaBypassToken(User user) => "";
        public bool ValidateCaptchaBypassToken(string encryptedToken, User user) => false;

        public Task<bool> ValidateCaptchaResponseAsync(string captchResponse, string clientIpAddress)
        {
            return Task.FromResult(true);
        }
    }
}
