namespace Bit.Core.Dirt.Services;

/// <summary>
/// Tracks the single active OAuth nonce for an integration so each OAuth <c>state</c> can be redeemed at most once.
/// </summary>
public interface IIntegrationOAuthNonceStore
{
    /// <summary>
    /// Generates a new nonce for the integration, replacing (and thereby invalidating) any previously issued nonce.
    /// </summary>
    /// <param name="integrationId">The integration the OAuth flow is being started for.</param>
    /// <returns>The new nonce, to be embedded in the protected OAuth state.</returns>
    Task<string> IssueAsync(Guid integrationId);

    /// <summary>
    /// Consumes the integration's active nonce if it matches the provided value.
    /// A non-matching nonce leaves the active nonce in place so a stale state cannot cancel a newer flow.
    /// </summary>
    /// <param name="integrationId">The integration referenced by the OAuth state.</param>
    /// <param name="nonce">The nonce carried by the OAuth state.</param>
    /// <returns><c>true</c> if the nonce was active and has now been consumed; otherwise <c>false</c>.</returns>
    Task<bool> TryConsumeAsync(Guid integrationId, string nonce);
}
