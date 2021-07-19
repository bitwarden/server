using System.Threading.Tasks;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public class NoopCaptchaValidationService : ICaptchaValidationService
    {
        public bool ServiceEnabled => false;
        public string SiteKey => null;
        public bool RequireCaptcha => false;

        public string GenerateCaptchaBypassToken(User user) => "";
        public bool ValidateCaptchaBypassToken(string encryptedToken, User user) => false;

        public Task<bool> ValidateCaptchaResponseAsync(string captchResponse, string clientIpAddress)
        {
            return Task.FromResult(true);
        }
    }
}
