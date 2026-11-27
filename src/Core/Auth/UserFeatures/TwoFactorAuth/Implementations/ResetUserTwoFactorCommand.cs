using Bit.Core.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Auth.UserFeatures.TwoFactorAuth.Implementations;

public class ResetUserTwoFactorCommand(
    IUserRepository userRepository,
    IRevokeTwoFactorRememberTokensCommand revokeTwoFactorRememberTokensCommand,
    TimeProvider timeProvider) : IResetUserTwoFactorCommand
{
    public async Task ResetAsync(User user)
    {
        // Sits before ReplaceAsync so a failure here leaves 2FA intact and a retry re-runs both steps.
        await revokeTwoFactorRememberTokensCommand.RevokeAllForUserAsync(user.Id);

        user.TwoFactorProviders = null;
        user.TwoFactorRecoveryCode = null;
        user.RevisionDate = user.AccountRevisionDate = timeProvider.GetUtcNow().UtcDateTime;
        await userRepository.ReplaceAsync(user);
    }
}
