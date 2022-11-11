using Bit.Core.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Services;

public class DeviceService : IDeviceService
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IPushRegistrationService _pushRegistrationService;

    public DeviceService(
        IDeviceRepository deviceRepository,
        IPushRegistrationService pushRegistrationService)
    {
        _deviceRepository = deviceRepository;
        _pushRegistrationService = pushRegistrationService;
    }

    public async Task<Device> SaveAsync(Device device)
    {
        Device result = null;
        if (device.Id == default(Guid))
        {
            result = await _deviceRepository.CreateAsync(device);
        }
        else
        {
            device.RevisionDate = DateTime.UtcNow;
            await _deviceRepository.ReplaceAsync(device);
            result = device;
        }
        return result;
    }

    public async Task ClearTokenAsync(Device device)
    {
        await _deviceRepository.ClearPushTokenAsync(device.Id);
        await _pushRegistrationService.DeleteRegistrationAsync(device.Id.ToString());
    }

    public async Task DeleteAsync(Device device)
    {
        await _deviceRepository.DeleteAsync(device);
        await _pushRegistrationService.DeleteRegistrationAsync(device.Id.ToString());
    }
}
