using Bit.Core.Auth.UserFeatures.Devices.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Auth.UserFeatures.Devices;

public class UpdateDeviceLastActivityCommand : IUpdateDeviceLastActivityCommand
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IDeviceLastActivityCacheService _cache;

    public UpdateDeviceLastActivityCommand(
        IDeviceRepository deviceRepository,
        IDeviceLastActivityCacheService cache)
    {
        _deviceRepository = deviceRepository;
        _cache = cache;
    }

    public async Task UpdateAsync(Device device, string? clientVersion)
    {
        if (await _cache.IsUpToDateAsync(device.UserId, device.Identifier, clientVersion))
        {
            return;
        }

        await _deviceRepository.UpdateLastActivityByIdAsync(device.Id, clientVersion);
        await _cache.RecordUpdateAsync(device.UserId, device.Identifier, clientVersion);
    }

    public async Task UpdateByIdentifierAndUserIdAsync(string identifier, Guid userId, string? clientVersion)
    {
        if (await _cache.IsUpToDateAsync(userId, identifier, clientVersion))
        {
            return;
        }

        await _deviceRepository.UpdateLastActivityByIdentifierAndUserIdAsync(identifier, userId, clientVersion);
        await _cache.RecordUpdateAsync(userId, identifier, clientVersion);
    }
}
