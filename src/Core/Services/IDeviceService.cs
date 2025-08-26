using Bit.Core.Auth.Models.Api.Request;
using Bit.Core.Entities;
using Bit.Core.NotificationHub;

namespace Bit.Core.Services;

public interface IDeviceService
{
    Task SaveAsync(WebPushRegistrationData webPush, Device device, IEnumerable<string> organizationIds);
    Task SaveAsync(Device device);
    Task ClearTokenAsync(Device device);
    Task DeactivateAsync(Device device);
    Task UpdateDevicesTrustAsync(string currentDeviceIdentifier,
        Guid currentUserId,
        DeviceKeysUpdateRequestModel currentDeviceUpdate,
        IEnumerable<OtherDeviceKeysUpdateRequestModel> alteredDevices);
}
