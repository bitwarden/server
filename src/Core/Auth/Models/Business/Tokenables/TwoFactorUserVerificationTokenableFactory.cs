using Bit.Core.Auth.Enums;
using Bit.Core.Entities;
using Bit.Core.Settings;

namespace Bit.Core.Auth.Models.Business.Tokenables;

/// <inheritdoc />
public class TwoFactorUserVerificationTokenableFactory : ITwoFactorUserVerificationTokenableFactory
{
    private readonly IGlobalSettings _globalSettings;

    public TwoFactorUserVerificationTokenableFactory(IGlobalSettings globalSettings)
    {
        _globalSettings = globalSettings;
    }

    /// <inheritdoc />
    public TwoFactorUserVerificationTokenable CreateToken(User user, TwoFactorProviderType providerType) =>
        new()
        {
            UserId = user.Id,
            ProviderType = providerType,
            ExpirationDate = DateTime.UtcNow.Add(
                TimeSpan.FromMinutes(_globalSettings.TwoFactorUserVerificationTokenLifetimeInMinutes)),
        };
}
