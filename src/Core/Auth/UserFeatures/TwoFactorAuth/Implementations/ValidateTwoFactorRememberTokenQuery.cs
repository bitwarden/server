using Bit.Core.Auth.Models.Business.Tokenables;
using Bit.Core.Auth.Repositories;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Tokens;

namespace Bit.Core.Auth.UserFeatures.TwoFactorAuth.Implementations;

public class ValidateTwoFactorRememberTokenQuery(
    ITwoFactorRememberTokenRepository twoFactorRememberTokenRepository,
    IDataProtectorTokenFactory<TwoFactorRememberTokenable> tokenFactory,
    ITwoFactorIsEnabledQuery twoFactorIsEnabledQuery,
    TimeProvider timeProvider) : IValidateTwoFactorRememberTokenQuery
{
    public async Task<bool> ValidateAsync(User user, string deviceIdentifier, string token)
    {
        // Covers tampering, a wrong data-protection purpose, the token's own expiry, and the
        // presence checks on its fields.
        if (!tokenFactory.TryUnprotect(token, out var tokenable) || tokenable is null || !tokenable.Valid)
        {
            return false;
        }

        if (tokenable.UserId != user.Id)
        {
            return false;
        }

        // Account-scoped: a security stamp rotation invalidates every token carrying the old value.
        if (!string.Equals(tokenable.SecurityStamp, user.SecurityStamp, StringComparison.Ordinal))
        {
            return false;
        }

        // Device binding. Case-insensitive because Device_ReadByIdentifierUserId matches the same
        // value through a case-insensitive collation, and a stricter comparison here would reject a
        // token whose identifier still resolves to the same device row.
        if (!string.Equals(tokenable.DeviceIdentifier, deviceIdentifier, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // A remember token stands in for a second factor, so it cannot be honored by an account that
        // has no second factor configured. Ordered ahead of the row read because a user with no
        // enabled providers resolves without a database call.
        if (!await twoFactorIsEnabledQuery.TwoFactorIsEnabledAsync(user))
        {
            return false;
        }

        // Safe to locate the row by the token's own DeviceId only because the identifier comparison
        // above already proved the presenter is on the device the token names. Do not reorder.
        var row = await twoFactorRememberTokenRepository.GetByUserIdDeviceIdAsync(user.Id, tokenable.DeviceId);
        if (row is null)
        {
            return false;
        }

        // Device-scoped: rotating the row's stamp cuts off this device without affecting the user's
        // other devices or any of their sessions.
        if (!string.Equals(tokenable.Stamp, row.Stamp, StringComparison.Ordinal))
        {
            return false;
        }

        return row.ExpirationDate > timeProvider.GetUtcNow().UtcDateTime;
    }
}
