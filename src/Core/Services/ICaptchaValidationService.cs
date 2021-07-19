using System.Threading.Tasks;
using Bit.Core.Models.Table;

namespace Bit.Core.Services
{
    public interface ICaptchaValidationService
    {
        bool ServiceEnabled { get; }
        string SiteKey { get; }
        bool RequireCaptcha { get; }
        Task<bool> ValidateCaptchaResponseAsync(string captchResponse, string clientIpAddress);
        string GenerateCaptchaBypassToken(User user);
        bool ValidateCaptchaBypassToken(string encryptedToken, User user);
    }
}
