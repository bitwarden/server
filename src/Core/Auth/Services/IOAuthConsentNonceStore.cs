namespace Bit.Core.Auth.Services;

/// <summary>
/// Issues and single-use-consumes nonces for the OIDC "Login with Bitwarden" consent handshake. A nonce is
/// issued when the user approves consent (bound into an <c>OAuthConsentTokenable</c>) and consumed exactly
/// once at the consent callback, preventing replay of a captured consent token.
/// </summary>
public interface IOAuthConsentNonceStore
{
    /// <summary>Issues a new single-use nonce and parks it in the distributed cache for the token lifetime.</summary>
    Task<string> IssueAsync();

    /// <summary>Returns true and removes the nonce if it is present; false otherwise (already used/expired).</summary>
    Task<bool> TryConsumeAsync(string nonce);
}
