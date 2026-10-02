using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Tokens;

namespace Bit.Core.Auth.UserFeatures.TwoFactorAuth.Implementations;

public class IssueTwoFactorRememberTokenCommand(
    ITwoFactorRememberTokenRepository twoFactorRememberTokenRepository,
    ITwoFactorRememberTokenableFactory tokenableFactory,
    IDataProtectorTokenFactory<TwoFactorRememberTokenable> tokenFactory,
    TimeProvider timeProvider) : IIssueTwoFactorRememberTokenCommand
{
    public async Task<string> IssueAsync(User user, Device device)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Generated here rather than by the repository because the value has to go into the token
        // as well as the row. Guid.NewGuid rather than a comb: this is an opaque comparand, not a
        // table identifier, and it should carry no embedded timestamp.
        var stamp = Guid.NewGuid().ToString();

        // The row takes its expiry from the token, so the two always lapse together.
        var tokenable = tokenableFactory.CreateToken(user, device, stamp);

        await twoFactorRememberTokenRepository.UpsertAsync(new TwoFactorRememberToken
        {
            UserId = user.Id,
            DeviceId = device.Id,
            Stamp = stamp,
            CreationDate = now,
            RevisionDate = now,
            ExpirationDate = tokenable.ExpirationDate,
        });

        return tokenFactory.Protect(tokenable);
    }
}
