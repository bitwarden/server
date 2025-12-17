namespace Bit.Core.AdminConsole.Models.Data.EventIntegrations;

/// <summary>
/// Represents the result of an integration handler operation, including success status,
/// failure categorization, and retry metadata. Use the <see cref="Succeed"/> factory method
/// for successful operations or <see cref="Fail"/> for failures with automatic retry-ability
/// determination based on the failure category.
/// </summary>
public class IntegrationHandlerResult
{
    /// <summary>
    /// True if the integration send succeeded, false otherwise.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// The integration message that was processed.
    /// </summary>
    public IIntegrationMessage Message { get; }

    /// <summary>
    /// Optional UTC date/time indicating when a failed operation should be retried.
    /// Will be used by the retry queue to delay re-sending the message.
    /// Usually set based on the Retry-After header from rate-limited responses.
    /// </summary>
    public DateTime? DelayUntilDate { get; private init; }

    /// <summary>
    /// Category of the failure. Null for successful results.
    /// </summary>
    public IntegrationFailureCategory? Category { get; private init; }

    /// <summary>
    /// Detailed failure reason or error message. Empty for successful results.
    /// </summary>
    public string? FailureReason { get; private init; }

    /// <summary>
    /// Indicates whether the operation is retryable.
    /// Computed from the failure category.
    /// </summary>
    public bool Retryable => Category switch
    {
        IntegrationFailureCategory.RateLimited => true,
        IntegrationFailureCategory.TransientError => true,
        IntegrationFailureCategory.ServiceUnavailable => true,
        IntegrationFailureCategory.AuthenticationFailed => false,
        IntegrationFailureCategory.ConfigurationError => false,
        IntegrationFailureCategory.PermanentFailure => false,
        null => false,
        _ => false
    };

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static IntegrationHandlerResult Succeed(IIntegrationMessage message)
    {
        return new IntegrationHandlerResult(success: true, message: message);
    }

    /// <summary>
    /// Creates a failed result with a failure category and reason.
    /// </summary>
    public static IntegrationHandlerResult Fail(
        IIntegrationMessage message,
        IntegrationFailureCategory category,
        string failureReason,
        DateTime? delayUntil = null)
    {
        return new IntegrationHandlerResult(success: false, message: message)
        {
            Category = category,
            FailureReason = failureReason,
            DelayUntilDate = delayUntil
        };
    }

    private IntegrationHandlerResult(bool success, IIntegrationMessage message)
    {
        Success = success;
        Message = message;
    }
}
