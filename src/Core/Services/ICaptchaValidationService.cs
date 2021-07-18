using System.Threading.Tasks;

namespace Bit.Core.Services
{
    public interface ICaptchaValidationService
    {
        bool ServiceEnabled { get; }
        string SiteKey { get; }
        bool RequireCaptcha { get; }
        Task<bool> ValidateCaptchaResponseAsync(string captchResponse, string clientIpAddress);
    }
}
