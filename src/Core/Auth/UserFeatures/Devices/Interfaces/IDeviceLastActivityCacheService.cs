namespace Bit.Core.Auth.UserFeatures.Devices.Interfaces;

public interface IDeviceLastActivityCacheService
{
    Task<bool> HasBeenBumpedTodayAsync(string identifier);
    Task RecordBumpAsync(string identifier);
}
