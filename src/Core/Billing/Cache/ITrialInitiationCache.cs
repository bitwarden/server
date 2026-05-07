namespace Bit.Core.Billing.Cache;

/// <summary>
/// Short-lived cache (15-minute TTL) that binds a trial length to a unique ID issued when a trial
/// initiation email is sent. Entries are removed after successful validation to enforce single use.
/// A cache miss is treated as a no-op (non-trial-email signup path).
/// </summary>
public interface ITrialInitiationCache
{
    /// <summary>Stores <paramref name="trialLength"/> keyed by <paramref name="trialInitiationId"/>.</summary>
    Task WriteAsync(string trialInitiationId, int trialLength);

    /// <summary>
    /// Confirms <paramref name="requestedTrialLength"/> matches the cached value and removes the entry.
    /// Throws <see cref="Bit.Core.Exceptions.BadRequestException"/> on a mismatch.
    /// </summary>
    Task ValidateTrialLengthAsync(string trialInitiationId, int requestedTrialLength);
}
