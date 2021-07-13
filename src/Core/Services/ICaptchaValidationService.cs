using System.Threading.Tasks;

namespace Bit.Core.Services
{
    public interface ICaptchaValidationService
    {
        bool ServiceEnabled { get; }
        string SiteKey { get; }
        Task<bool> ValidateCaptchaResponseAsync(string captchResponse, string clientIpAddress);
    }
}
