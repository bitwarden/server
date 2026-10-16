namespace Bit.Api.AdminConsole.Partnerships;

/// <summary>
/// Request counters for <see cref="PartnershipRateLimitFilter"/>, shared across API instances when a distributed store
/// is configured.
/// </summary>
public interface IPartnershipRateLimitStore
{
    /// <summary>
    /// Increments the counter for <paramref name="key"/> and returns its new value. A new counter expires after
    /// <paramref name="expiry"/>. Throws when the store is unavailable.
    /// </summary>
    Task<long> IncrementAsync(string key, TimeSpan expiry);
}
