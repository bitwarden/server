using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Identity.Utilities;

public static class LoginApprovingDeviceTypes
{
    private static readonly IReadOnlyCollection<DeviceType> _deviceTypes;

    static LoginApprovingDeviceTypes()
    {
        var deviceTypes = new List<DeviceType>();
        deviceTypes.AddRange(DeviceTypes.DesktopTypes);
        deviceTypes.AddRange(DeviceTypes.MobileTypes);
        deviceTypes.AddRange(DeviceTypes.BrowserTypes);
        _deviceTypes = deviceTypes.AsReadOnly();
    }

    public static IReadOnlyCollection<DeviceType> Types => _deviceTypes;
}
