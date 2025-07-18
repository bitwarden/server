using Microsoft.Extensions.Caching.Distributed;

namespace Bit.Core.Auth.Identity.TokenProviders;

/// <summary>
/// Options for configuring the OTP token provider.
/// </summary>
public class DefaultOtpTokenProviderOptions
{
    /// <summary>
    /// Gets or sets the length of the generated token.
    /// Default is 6 characters.
    /// </summary>
    public int TokenLength { get; set; } = 6;

    /// <summary>
    /// Gets or sets whether the token should contain alphabetic characters.
    /// Default is false.
    /// </summary>
    public bool TokenAlpha { get; set; } = false;

    /// <summary>
    /// Gets or sets whether the token should contain numeric characters.
    /// Default is true.
    /// </summary>
    public bool TokenNumeric { get; set; } = true;

    /// <summary>
    /// Cache entry options for Otp Token provider.
    /// Default is 5 minutes expiration.
    /// </summary>
    public DistributedCacheEntryOptions DistributedCacheEntryOptions { get; set; } = new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };
}
