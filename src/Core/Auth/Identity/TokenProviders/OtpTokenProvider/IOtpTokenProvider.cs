namespace Bit.Core.Auth.Identity.TokenProviders;

/// <summary>
/// A generic interface for a one-time password (OTP) token provider.
/// </summary>
public interface IOtpTokenProvider<TOptions>
    where TOptions : DefaultOtpTokenProviderOptions
{
    /// <summary>
    /// Generates a new one-time password (OTP) based on the configured parameters.
    /// The generated OTP is stored in the distributed cache with a key based on the unique identifier and purpose. If the
    /// key is already in use, it will overwrite and generate a new OTP with a refreshed TTL.
    /// </summary>
    /// <param name="tokenProviderName">Name of the token provider, used to distinguish different token providers that may inject this class</param>
    /// <param name="purpose">Purpose of the OTP token, used to distinguish different types of tokens.</param>
    /// <param name="uniqueIdentifier">Unique identifier to distinguish one request from another</param>
    /// <param name="boundValue">
    /// Optional value the generated token is bound to. When set, <see cref="ValidateTokenAsync"/> only
    /// succeeds if it is called with the same bound value, even if the token itself matches.
    /// </param>
    /// <returns>generated token | null</returns>
    Task<string?> GenerateTokenAsync(string tokenProviderName, string purpose, string uniqueIdentifier, string? boundValue = null);

    /// <summary>
    /// Validates the provided token against the stored value in the distributed cache.
    /// </summary>
    /// <param name="token">string value matched against the unique identifier in the cache if found</param>
    /// <param name="tokenProviderName">Name of the token provider, used to distinguish different token providers that may inject this class</param>
    /// <param name="purpose">Purpose of the OTP token, used to distinguish different types of tokens.</param>
    /// <param name="uniqueIdentifier">Unique identifier to distinguish one request from another</param>
    /// <param name="boundValue">
    /// Value the token must have been bound to at generation time. A token generated with a different bound
    /// value (or with none) does not validate, even when the token itself is correct.
    /// </param>
    /// <returns>true if the token matches what is fetched from the cache, false if not.</returns>
    Task<bool> ValidateTokenAsync(string token, string tokenProviderName, string purpose, string uniqueIdentifier, string? boundValue = null);

    // TODO: PM-43465 - Delete this member and its implementation once every supported client version sends
    // the Device-Identifier header on the new device verification resend request. It exists only to let that
    // resend fall back to the device a pending code was issued to.
    /// <summary>
    /// Returns the bound value the currently pending token was generated with, without consuming the token,
    /// or <see langword="null"/> if no token is pending.
    /// </summary>
    /// <param name="tokenProviderName">Name of the token provider, used to distinguish different token providers that may inject this class</param>
    /// <param name="purpose">Purpose of the OTP token, used to distinguish different types of tokens.</param>
    /// <param name="uniqueIdentifier">Unique identifier to distinguish one request from another</param>
    Task<string?> PeekBoundValueAsync(string tokenProviderName, string purpose, string uniqueIdentifier);
}
