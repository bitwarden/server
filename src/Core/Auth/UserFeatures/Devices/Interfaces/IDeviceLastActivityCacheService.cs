namespace Bit.Core.Auth.UserFeatures.Devices.Interfaces;

public interface IDeviceLastActivityCacheService
{
    Task<bool> HasBeenBumpedTodayAsync(Guid userId, string identifier);
    Task RecordBumpAsync(Guid userId, string identifier);
}
