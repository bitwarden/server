namespace Bit.Core.Auth.Models.Business.Tokenables;

/// <summary>Mints <see cref="OAuthConsentTokenable"/> instances with the configured single-use lifetime.</summary>
public interface IOAuthConsentTokenableFactory
{
    /// <summary>
    /// Creates a consent token bound to <paramref name="userId"/> and the authorize request identified by
    /// <paramref name="returnUrl"/>, carrying a single-use <paramref name="nonce"/>.
    /// </summary>
    OAuthConsentTokenable CreateToken(Guid userId, string returnUrl, string nonce);
}
