namespace Bit.Core.Auth.UserFeatures.WebAuthnLogin;

/// <summary>
/// Enforces single-use semantics for WebAuthn assertion challenges per the W3C WebAuthn
/// specification (§13.4.3). A challenge must be stored when assertion options are created
/// and consumed when the assertion is validated.
/// </summary>
public interface IWebAuthnChallengeCacheProvider
{
    /// <summary>
    /// Stores a WebAuthn challenge in the cache, marking it as available for a single use.
    /// Must be called when assertion options are generated (the first step of the WebAuthn login flow).
    /// </summary>
    /// <param name="challenge">The challenge bytes from <see cref="Fido2NetLib.AssertionOptions.Challenge"/>.</param>
    Task StoreChallengeAsync(byte[] challenge);

    /// <summary>
    /// Atomically checks and removes a challenge from the cache.
    /// Returns <c>true</c> if the challenge existed and <c>false</c> if it was already consumed
    /// or not found. The challenge is removed regardless of the caller's subsequent success or
    /// failure, consistent with WebAuthn's one-time-use requirement.
    /// </summary>
    /// <param name="challenge">The challenge bytes from the token returned in the first step.</param>
    Task<bool> ConsumeChallengeAsync(byte[] challenge);
}
