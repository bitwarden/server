namespace Bit.Core.Dirt.Enums;

/// <summary>
/// Categories of event integration failures used for classification and retry logic.
/// </summary>
public enum IntegrationFailureCategory : int
{
    /// <summary>
    /// Service is temporarily unavailable (503, upstream outage, maintenance).
    /// </summary>
    ServiceUnavailable = 0,

    /// <summary>
    /// Authentication failed (401, 403, invalid_auth, token issues).
    /// </summary>
    AuthenticationFailed = 1,

    /// <summary>
    /// Configuration error (invalid config, channel_not_found, etc.).
    /// </summary>
    ConfigurationError = 2,

    /// <summary>
    /// Rate limited (429, rate_limited).
    /// </summary>
    RateLimited = 3,

    /// <summary>
    /// Transient error (timeouts, 500, network errors).
    /// </summary>
    TransientError = 4,

    /// <summary>
    /// Permanent failure unrelated to authentication/config (e.g., unrecoverable payload/format issue).
    /// </summary>
    PermanentFailure = 5
}
