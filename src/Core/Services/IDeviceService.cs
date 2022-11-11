using Bit.Core.Entities;

namespace Bit.Core.Services;

public interface IDeviceService
{
    Task<Device> SaveAsync(Device device);
    Task DeleteAsync(Device device);
}
