using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Bit.Core.Tokens;
using Bit.Core.Utilities;

namespace Bit.Core.Auth.Models.Business.Tokenables;

/// <summary>
/// Short-lived, single-use proof that a user authenticated in the web vault and approved consent for a
/// specific pending OIDC authorize request. Minted after the user approves, then presented back to
/// Identity's consent callback to sign the user in and grant consent for the "Login with Bitwarden"
/// partner flow.
/// </summary>
/// <remarks>
/// The token is data-protected so it can neither be read nor forged by a client. It binds to the user and
/// to a hash of the exact authorize request (<see cref="AuthorizationRequestHash"/>), and carries a
/// single-use <see cref="Nonce"/> matched against <c>IOAuthConsentNonceStore</c>. It deliberately carries
/// no PSK and grants no vault access. Minted only through <see cref="IOAuthConsentTokenableFactory"/>.
/// </remarks>
public class OAuthConsentTokenable : ExpiringTokenable
{
    public const string ClearTextPrefix = "BwOAuthConsent";
    public const string DataProtectorPurpose = "OAuthConsentDataProtector";
    public const string TokenIdentifier = "OAuthConsentToken";
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    [JsonInclude]
    public string Identifier { get; internal set; } = TokenIdentifier;

    [JsonInclude]
    public Guid UserId { get; internal set; }

    /// <summary>Hash of the authorize request (its <c>returnUrl</c>) this consent is bound to.</summary>
    [JsonInclude]
    public string AuthorizationRequestHash { get; internal set; } = string.Empty;

    /// <summary>Single-use value matched against the active nonce in <c>IOAuthConsentNonceStore</c>.</summary>
    [JsonInclude]
    public string Nonce { get; internal set; } = string.Empty;

    // Leaves ExpirationDate at its default, so a token constructed without the factory is expired and
    // therefore never valid.
    [JsonConstructor]
    public OAuthConsentTokenable() { }

    internal OAuthConsentTokenable(Guid userId, string authorizationRequestHash, string nonce)
    {
        if (userId == default)
        {
            throw new ArgumentException("User id cannot be empty.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(authorizationRequestHash))
        {
            throw new ArgumentException("Authorization request hash cannot be null or whitespace.",
                nameof(authorizationRequestHash));
        }

        if (string.IsNullOrWhiteSpace(nonce))
        {
            throw new ArgumentException("Nonce cannot be null or whitespace.", nameof(nonce));
        }

        UserId = userId;
        AuthorizationRequestHash = authorizationRequestHash;
        Nonce = nonce;
    }

    /// <summary>
    /// Checks that the token was issued for exactly this user and authorize request. The caller is still
    /// responsible for consuming the <see cref="Nonce"/> through the nonce store.
    /// </summary>
    public bool IsValidFor(Guid userId, string returnUrl) =>
        Valid &&
        UserId == userId &&
        string.Equals(AuthorizationRequestHash, HashAuthorizationRequest(returnUrl), StringComparison.Ordinal);

    protected override bool TokenIsValid() =>
        Identifier == TokenIdentifier &&
        UserId != default &&
        !string.IsNullOrWhiteSpace(AuthorizationRequestHash) &&
        !string.IsNullOrWhiteSpace(Nonce);

    /// <summary>Stable hash of an authorize request's <c>returnUrl</c> used to bind a token to it.</summary>
    public static string HashAuthorizationRequest(string returnUrl)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(returnUrl ?? string.Empty));
        return CoreHelpers.Base64UrlEncode(bytes);
    }
}
