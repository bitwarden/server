namespace Bit.Core.Billing.Cache;

/// <summary>
/// Short-lived cache (15-minute TTL) that binds a trial length to a unique ID issued when a trial
/// initiation email is sent. Entries are removed on retrieval to enforce single use.
/// </summary>
public interface ITrialInitiationCache
{
    /// <summary>Stores <paramref name="trialLength"/> keyed by <paramref name="trialInitiationId"/>.</summary>
    Task WriteAsync(string trialInitiationId, int trialLength);

    /// <summary>
    /// Returns the cached trial length and removes the entry, or <c>null</c> if no entry exists.
    /// </summary>
    Task<int?> GetAndRemoveAsync(string trialInitiationId);
}
