using Bit.Core.Auth.Repositories;

namespace Bit.Core.Auth.UserFeatures.TwoFactorAuth.Implementations;

public class RevokeTwoFactorRememberTokensCommand(
    ITwoFactorRememberTokenRepository twoFactorRememberTokenRepository,
    TimeProvider timeProvider)
    : IRevokeTwoFactorRememberTokensCommand
{
    public Task RevokeAllForUserAsync(Guid userId) =>
        twoFactorRememberTokenRepository.RotateStampsByUserIdAsync(userId, timeProvider.GetUtcNow().UtcDateTime);
}
