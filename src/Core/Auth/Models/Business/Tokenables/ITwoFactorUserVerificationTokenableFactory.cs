using Bit.Core.Auth.Enums;
using Bit.Core.Entities;

namespace Bit.Core.Auth.Models.Business.Tokenables;

/// <summary>Mints <see cref="TwoFactorUserVerificationTokenable"/> instances with the operator-configured lifetime.</summary>
public interface ITwoFactorUserVerificationTokenableFactory
{
    /// <summary>Creates a token bound to the given user and provider, expiring after the configured lifetime.</summary>
    TwoFactorUserVerificationTokenable CreateToken(User user, TwoFactorProviderType providerType);
}
