using Bit.Core.Auth.Identity.TokenProviders;

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
    /// <param name="tokenProviderName">Name of the token provider, used to distinguish different token providers that may inject this class</param>
    /// <param name="purpose">Purpose of the OTP token, used to distinguish different types of tokens.</param>
    /// <param name="uniqueIdentifier">Unique identifier to distinguish one request from another</param>
    /// <returns>generated token</returns>
    Task<string> GenerateTokenAsync(string tokenProviderName, string purpose, string uniqueIdentifier);

    /// <summary>
    /// Validates the provided token against the stored value in the distributed cache.
    /// </summary>
    /// <param name="token">string value matched against the unique identifier in the cache if found</param>
    /// <param name="tokenProviderName">Name of the token provider, used to distinguish different token providers that may inject this class</param>
    /// <param name="purpose">Purpose of the OTP token, used to distinguish different types of tokens.</param>
    /// <param name="uniqueIdentifier">Unique identifier to distinguish one request from another</param>
    /// <returns>true if the token matches what is fetched from the cache, false if not.</returns>
    Task<bool> ValidateTokenAsync(string token, string tokenProviderName, string purpose, string uniqueIdentifier);

    /// <summary>
    /// Configures the token provider with the specified parameters.
    /// This method allows customization of the token length and character set.
    /// </summary>
    /// <param name="options">Configuration options for the OTP token provider.</param>
    /// <remarks>
    /// The default configuration is:
    /// - Token Length: 6 characters
    /// - Token Alpha: false (no alphabetic characters)
    /// - Token Numeric: true (numeric characters only)
    /// - Cache Entry Options: 5 minutes expiration
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if the options parameter is null.</exception>
    void ConfigureToken(OtpTokenProviderConfigurationOptions options);
}
