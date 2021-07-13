using System.Threading.Tasks;

namespace Bit.Core.Services
{
    public class NoopCaptchaValidationService : ICaptchaValidationService
    {
        public bool ServiceEnabled => false;
        public string SiteKey => null;

        public Task<bool> ValidateCaptchaResponseAsync(string captchResponse, string clientIpAddress)
        {
            return Task.FromResult(true);
        }
    }
}
