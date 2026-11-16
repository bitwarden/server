namespace Bit.Core.Auth.Models.Business.Tokenables;

/// <inheritdoc />
public class OAuthConsentTokenableFactory(TimeProvider timeProvider) : IOAuthConsentTokenableFactory
{
    /// <inheritdoc />
    public OAuthConsentTokenable CreateToken(Guid userId, string returnUrl, string nonce) =>
        new(userId, OAuthConsentTokenable.HashAuthorizationRequest(returnUrl), nonce)
        {
            ExpirationDate = timeProvider.GetUtcNow().UtcDateTime.Add(OAuthConsentTokenable.Lifetime),
            TimeProvider = timeProvider,
        };
}
