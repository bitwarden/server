using Bit.Core.Models.Api;

namespace Bit.Api.Models.Response;

[Obsolete("Leaving this for backwards compatibilty on clients")]
public class DeviceVerificationResponseModel : ResponseModel
{
    public DeviceVerificationResponseModel(bool isDeviceVerificationSectionEnabled, bool unknownDeviceVerificationEnabled)
        : base("deviceVerificationSettings")
    {
        IsDeviceVerificationSectionEnabled = isDeviceVerificationSectionEnabled;
        UnknownDeviceVerificationEnabled = unknownDeviceVerificationEnabled;
    }

    public bool IsDeviceVerificationSectionEnabled { get; }
    public bool UnknownDeviceVerificationEnabled { get; }
}
