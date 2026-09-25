using Bit.Core.Auth.Entities;

namespace Bit.Core.Auth.Repositories;

/// <summary>
/// Point lookups and scoped writes only, deliberately: no ad-hoc queries, so that every operation here
/// is expressible against a partitioned document store if this ever needs a second implementation.
/// </summary>
public interface ITwoFactorRememberTokenRepository
{
    /// <summary>
    /// Reads the row for one remembered device, or null if the device has never been remembered.
    /// </summary>
    Task<TwoFactorRememberToken?> GetByUserIdDeviceIdAsync(Guid userId, Guid deviceId);

    /// <summary>
    /// Creates the row for this user and device, or updates the existing one in place,
    /// preserving its <c>CreationDate</c>. The caller supplies a fresh <c>Stamp</c>, so any
    /// token previously issued for this device stops validating.
    /// </summary>
    Task<TwoFactorRememberToken> UpsertAsync(TwoFactorRememberToken token);

    /// <summary>
    /// Rotates the <c>Stamp</c> on every row for this user, invalidating all of their remember
    /// tokens at once. Rows are kept.
    /// </summary>
    /// <remarks>
    /// The new stamp is generated here rather than by the caller: nothing above this layer needs to
    /// know the value, and generating it in one place keeps every provider writing the same thing.
    /// </remarks>
    Task RotateStampsByUserIdAsync(Guid userId);

    /// <summary>
    /// Deletes every row that expired before <paramref name="now"/>. Hygiene only — an expired row is
    /// already refused at validation time.
    /// </summary>
    /// <param name="now">
    /// The instant to compare against, supplied by the caller so that every provider and the stored
    /// procedure agree on one value.
    /// </param>
    Task DeleteExpiredAsync(DateTime now);
}
