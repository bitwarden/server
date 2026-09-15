using Bit.Core.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Auth.UserFeatures.TwoFactorAuth.Implementations;

public class ResetUserTwoFactorCommand(
    IUserRepository userRepository,
    TimeProvider timeProvider) : IResetUserTwoFactorCommand
{
    public async Task ResetAsync(User user)
    {
        user.TwoFactorProviders = null;
        user.TwoFactorRecoveryCode = null;
        user.SecurityStamp = Guid.NewGuid().ToString();
        user.RevisionDate = user.AccountRevisionDate = timeProvider.GetUtcNow().UtcDateTime;
        await userRepository.ReplaceAsync(user);
    }
}
