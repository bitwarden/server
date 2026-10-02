using Bit.Core.Entities;

namespace Bit.Core.Auth.Models.Business.Tokenables;

/// <summary>Mints <see cref="TwoFactorRememberTokenable"/> instances with the operator-configured lifetime.</summary>
public interface ITwoFactorRememberTokenableFactory
{
    /// <summary>
    /// Creates a token bound to the given user and device, carrying <paramref name="stamp"/>, and expiring
    /// after the configured lifetime.
    /// </summary>
    TwoFactorRememberTokenable CreateToken(User user, Device device, string stamp);
}
