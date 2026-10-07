namespace Bit.Core.Auth.UserFeatures.WebAuthnLogin;

/// <summary>
/// Enforces single-use semantics for WebAuthn assertion challenges per the W3C WebAuthn
/// specification (§13.4.3). Tracks used challenges so that each challenge can only be
/// validated once.
/// </summary>
public interface IWebAuthnChallengeCacheProvider
{
    /// <summary>
    /// Attempts to mark a challenge as used. Returns <c>true</c> if this is the first use
    /// (challenge was not in cache and has now been saved). Returns <c>false</c> if the
    /// challenge was already marked as used (found in cache).
    /// </summary>
    /// <param name="challenge">The challenge bytes from <see cref="Fido2NetLib.AssertionOptions.Challenge"/>.</param>
    Task<bool> TryMarkChallengeAsUsedAsync(byte[] challenge);
}
