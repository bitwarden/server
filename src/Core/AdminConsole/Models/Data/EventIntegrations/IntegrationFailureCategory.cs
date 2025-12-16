namespace Bit.Core.AdminConsole.Models.Data.EventIntegrations;

/// <summary>
/// Categories of event integration failures used for classification and retry logic.
/// </summary>
public enum IntegrationFailureCategory
{
    /// <summary>
    /// Service is temporarily unavailable (503, upstream outage, maintenance). Retryable.
    /// </summary>
    ServiceUnavailable,

    /// <summary>
    /// Authentication failed (401, 403, invalid_auth, token issues). Not retryable.
    /// </summary>
    AuthenticationFailed,

    /// <summary>
    /// Configuration error (invalid config, channel_not_found, etc.). Not retryable.
    /// </summary>
    ConfigurationError,

    /// <summary>
    /// Rate limited (429, rate_limited). Retryable.
    /// </summary>
    RateLimited,

    /// <summary>
    /// Transient error (timeouts, 500, network errors). Retryable.
    /// </summary>
    TransientError,

    /// <summary>
    /// Permanent failure unrelated to authentication/config (e.g., unrecoverable payload/format issue).
    /// Not retryable.
    /// </summary>
    PermanentFailure
}
