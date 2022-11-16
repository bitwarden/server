using Bit.Core.Entities;

namespace Bit.Core.Services;

public interface IDeviceService
{
    Task SaveAsync(Device device);
    Task ClearTokenAsync(Device device);
    Task DeleteAsync(Device device);

}
