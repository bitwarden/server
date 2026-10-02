using Bit.Core.Entities;
using Bit.Core.Settings;

namespace Bit.Core.Auth.Models.Business.Tokenables;

/// <inheritdoc />
public class TwoFactorRememberTokenableFactory(
    IGlobalSettings globalSettings,
    TimeProvider timeProvider) : ITwoFactorRememberTokenableFactory
{
    /// <inheritdoc />
    public TwoFactorRememberTokenable CreateToken(User user, Device device, string stamp) =>
        new(user.Id, device.Id, device.Identifier, stamp, user.SecurityStamp)
        {
            ExpirationDate = timeProvider.GetUtcNow().UtcDateTime
                .AddDays(globalSettings.TwoFactorRememberTokenLifetimeInDays),
        };
}
