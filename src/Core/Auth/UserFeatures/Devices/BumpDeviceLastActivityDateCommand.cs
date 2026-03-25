using Bit.Core.Auth.UserFeatures.Devices.Interfaces;
using Bit.Core.Repositories;

namespace Bit.Core.Auth.UserFeatures.Devices;

public class BumpDeviceLastActivityDateCommand : IBumpDeviceLastActivityDateCommand
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceLastActivityCacheService _lastActivityCache;

    public BumpDeviceLastActivityDateCommand(
        IDeviceRepository deviceRepository,
        IDeviceLastActivityCacheService lastActivityCache)
    {
        _deviceRepository = deviceRepository;
        _lastActivityCache = lastActivityCache;
    }

    public async Task BumpByIdAsync(Guid deviceId, string identifier, Guid userId)
    {
        if (await _lastActivityCache.HasBeenBumpedTodayAsync(userId, identifier))
        {
            return;
        }

        await _deviceRepository.BumpLastActivityDateByIdAsync(deviceId);
        await _lastActivityCache.RecordBumpAsync(userId, identifier);
    }

    public async Task BumpByIdentifierAsync(string identifier, Guid userId)
    {
        if (await _lastActivityCache.HasBeenBumpedTodayAsync(userId, identifier))
        {
            return;
        }

        await _deviceRepository.BumpLastActivityDateByIdentifierAsync(identifier, userId);
        await _lastActivityCache.RecordBumpAsync(userId, identifier);
    }
}
