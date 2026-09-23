using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Repositories;
using Bit.Core.Entities;
using Bit.Core.Tokens;

namespace Bit.Core.Auth.UserFeatures.TwoFactorAuth.Implementations;

public class IssueTwoFactorRememberTokenCommand(
    ITwoFactorRememberTokenRepository twoFactorRememberTokenRepository,
    IDataProtectorTokenFactory<TwoFactorRememberTokenable> tokenFactory,
    TimeProvider timeProvider) : IIssueTwoFactorRememberTokenCommand
{
    public async Task<string> IssueAsync(User user, Device device)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expirationDate = now.Add(TwoFactorRememberTokenable.GetTokenLifetime());

        // Generated here rather than by the repository because the value has to go into the token
        // as well as the row. Guid.NewGuid rather than a comb: this is an opaque comparand, not a
        // table identifier, and it should carry no embedded timestamp.
        var stamp = Guid.NewGuid().ToString();

        var row = await twoFactorRememberTokenRepository.UpsertAsync(new TwoFactorRememberToken
        {
            UserId = user.Id,
            DeviceId = device.Id,
            Stamp = stamp,
            CreationDate = now,
            RevisionDate = now,
            ExpirationDate = expirationDate,
        });

        return tokenFactory.Protect(new TwoFactorRememberTokenable
        {
            UserId = user.Id,
            DeviceId = device.Id,
            DeviceIdentifier = device.Identifier,
            Stamp = row.Stamp,
            SecurityStamp = user.SecurityStamp,
            ExpirationDate = row.ExpirationDate,
        });
    }
}
