using Bit.Core.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Services;

public class DeviceService : IDeviceService
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IPushRegistrationService _pushRegistrationService;

    public DeviceService(
        IDeviceRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
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

    public async Task DeleteAsync(Device device)
    {
        await _deviceRepository.DeleteAsync(device);
        await _pushRegistrationService.DeleteRegistrationAsync(device.Id.ToString());
    }
}
