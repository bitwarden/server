namespace Bit.Core.AdminConsole.Models.Data.EventIntegrations;

/// <summary>
/// Categories of event integration failures used for classification and retry logic.
/// </summary>
public enum IntegrationFailureCategory
{
    /// <summary>
    /// Service is temporarily unavailable (503, upstream outage, maintenance).
    /// </summary>
    ServiceUnavailable,

    /// <summary>
    /// Authentication failed (401, 403, invalid_auth, token issues).
    /// </summary>
    AuthenticationFailed,

    /// <summary>
    /// Configuration error (invalid config, channel_not_found, etc.).
    /// </summary>
    ConfigurationError,

    /// <summary>
    /// Rate limited (429, rate_limited).
    /// </summary>
    RateLimited,

    /// <summary>
    /// Transient error (timeouts, 500, network errors).
    /// </summary>
    TransientError,

    /// <summary>
    /// Permanent failure unrelated to authentication/config (e.g., unrecoverable payload/format issue).
    /// </summary>
    PermanentFailure
}
