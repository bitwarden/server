using Bit.Core.Entities;

namespace Bit.Core.Auth.Services;

public interface ITwoFactorEmailService
{
    Task SendTwoFactorEmailAsync(User user);
    Task SendTwoFactorSetupEmailAsync(User user);
    Task SendNewDeviceVerificationEmailAsync(User user);
    Task<bool> VerifyTwoFactorTokenAsync(User user, string token);
}
