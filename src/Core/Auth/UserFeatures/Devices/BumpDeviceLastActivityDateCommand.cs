using Bit.Core.Auth.UserFeatures.Devices.Interfaces;
using Bit.Core.Entities;
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

    public async Task BumpAsync(Device device)
    {
        if (await _lastActivityCache.HasBeenBumpedTodayAsync(device.UserId, device.Identifier))
        {
            return;
        }

        await _deviceRepository.BumpLastActivityDateByIdAsync(device.Id);
        await _lastActivityCache.RecordBumpAsync(device.UserId, device.Identifier);
    }

    public async Task BumpByIdentifierAndUserIdAsync(string identifier, Guid userId)
    {
        if (await _lastActivityCache.HasBeenBumpedTodayAsync(userId, identifier))
        {
            return;
        }

        await _deviceRepository.BumpLastActivityDateByIdentifierAndUserIdAsync(identifier, userId);
        await _lastActivityCache.RecordBumpAsync(userId, identifier);
    }
}
