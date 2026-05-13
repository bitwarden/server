using Bit.Core.Auth.UserFeatures.Devices.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Auth.UserFeatures.Devices;

public class BumpDeviceDataCommand : IBumpDeviceDataCommand
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceDataCacheService _cache;

    public BumpDeviceDataCommand(
        IDeviceRepository deviceRepository,
        IDeviceDataCacheService cache)
    {
        _deviceRepository = deviceRepository;
        _cache = cache;
    }

    public async Task BumpAsync(Device device, string? clientVersion)
    {
        if (await _cache.IsUpToDateAsync(device.UserId, device.Identifier, clientVersion))
        {
            return;
        }

        await _deviceRepository.BumpDeviceDataByIdAsync(device.Id, clientVersion);
        await _cache.RecordBumpAsync(device.UserId, device.Identifier, clientVersion);
    }

    public async Task BumpByIdentifierAndUserIdAsync(string identifier, Guid userId, string? clientVersion)
    {
        if (await _cache.IsUpToDateAsync(userId, identifier, clientVersion))
        {
            return;
        }

        await _deviceRepository.BumpDeviceDataByIdentifierAndUserIdAsync(identifier, userId, clientVersion);
        await _cache.RecordBumpAsync(userId, identifier, clientVersion);
    }
}
