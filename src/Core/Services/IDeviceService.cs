using Bit.Core.Auth.Models.Api.Request;
using Bit.Core.Entities;

namespace Bit.Core.Services;

public interface IDeviceService
{
    Task SaveAsync(Device device);
    Task ClearTokenAsync(Device device);
    Task DeactivateAsync(Device device);
    Task UpdateDevicesTrustAsync(
        string currentDeviceIdentifier,
        Guid currentUserId,
        DeviceKeysUpdateRequestModel currentDeviceUpdate,
        IEnumerable<OtherDeviceKeysUpdateRequestModel> alteredDevices
    );
}
