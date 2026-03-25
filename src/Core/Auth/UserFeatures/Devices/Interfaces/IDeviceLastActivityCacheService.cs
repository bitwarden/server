namespace Bit.Core.Auth.UserFeatures.Devices.Interfaces;

/// <summary>
/// Distributed cache for tracking whether a device's <c>LastActivityDate</c> has already been
/// bumped today, preventing redundant database writes within the same calendar day.
/// Cache keys are scoped to <c>(userId, identifier)</c> because device identifiers are only
/// unique per user, not globally.
/// </summary>
public interface IDeviceLastActivityCacheService
{
    /// <summary>Returns <c>true</c> if the device has already been bumped today.</summary>
    Task<bool> HasBeenBumpedTodayAsync(Guid userId, string identifier);

    /// <summary>Records that the device has been bumped today.</summary>
    Task RecordBumpAsync(Guid userId, string identifier);
}
