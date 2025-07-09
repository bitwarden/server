using Microsoft.Extensions.Caching.Distributed;

namespace Core.Auth.Identity.TokenProviders;

/// <summary>
/// A generic interface for a one-time password (OTP) token provider.
/// </summary>
public interface IOtpTokenProvider
{
    /// <summary>
    /// Generates a new one-time password (OTP) based on the configured parameters.
    /// The generated OTP is stored in the distributed cache with a key based on the unique identifier and purpose. If the
    /// key is already in use, it will overwrite and generate a new OTP with a refreshed TTL.
    /// </summary>
    /// <param name="purpose">Purpose of the OTP token, used to distinguish different types of tokens.</param>
    /// <param name="uniqueIdentifier">Unique identifier to distinguish one request from another</param>
    /// <returns>generated token</returns>
    Task<string> GenerateTokenAsync(string purpose, string uniqueIdentifier);

    /// <summary>
    /// Validates the provided token against the stored value in the distributed cache.
    /// </summary>
    /// <param name="token">string value matched against the unique identifier in the cache if found</param>
    /// <param name="purpose">Purpose of the OTP token, used to distinguish different types of tokens.</param>
    /// <param name="uniqueIdentifier">Unique identifier to distinguish one request from another</param>
    /// <returns>true if the token matches what is fetched from the cache, false if not.</returns>
    Task<bool> ValidateTokenAsync(string token, string purpose, string uniqueIdentifier);

    /// <summary>
    /// Configures the token provider with the specified parameters.
    /// This method allows customization of the token length and character set.
    /// </summary>
    /// <param name="length">The length of the generated token.</param>
    /// <param name="alpha">Whether the token should contain alphabetic characters.</param>
    /// <param name="numeric">Whether the token should contain numeric characters.</param>
    void ConfigureToken(int length, bool alpha, bool numeric);

    /// <summary>
    /// Sets the cache entry options for the token provider.
    /// This method allows customization of the cache entry options, such as expiration time.
    /// </summary>
    /// <param name="options">The cache entry options to set.</param>
    void SetCacheEntryOptions(DistributedCacheEntryOptions options);
}
