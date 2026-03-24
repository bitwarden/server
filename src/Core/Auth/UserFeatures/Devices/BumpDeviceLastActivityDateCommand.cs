using Bit.Core.Auth.UserFeatures.Devices.Interfaces;
using Bit.Core.Repositories;

namespace Bit.Core.Auth.UserFeatures.Devices;

public class BumpDeviceLastActivityDateCommand : IBumpDeviceLastActivityDateCommand
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceLastActivityCacheService _activityCache;

    public BumpDeviceLastActivityDateCommand(
        IDeviceRepository deviceRepository,
        IDeviceLastActivityCacheService activityCache)
    {
        _deviceRepository = deviceRepository;
        _activityCache = activityCache;
    }

    public async Task BumpByIdAsync(Guid deviceId, string identifier)
    {
        if (await _activityCache.HasBeenBumpedTodayAsync(identifier))
        {
            return;
        }

        await _deviceRepository.BumpLastActivityDateByIdAsync(deviceId);
        await _activityCache.RecordBumpAsync(identifier);
    }

    public async Task BumpByIdentifierAsync(string identifier, Guid userId)
    {
        if (await _activityCache.HasBeenBumpedTodayAsync(identifier))
        {
            return;
        }

        await _deviceRepository.BumpLastActivityDateByIdentifierAsync(identifier, userId);
        await _activityCache.RecordBumpAsync(identifier);
    }
}
