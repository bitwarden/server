using Bit.Core.Enums;

namespace Bit.Identity.Utilities;

public static class ApprovingDeviceTypes
{
    private static readonly IReadOnlyCollection<DeviceType> _deviceTypes = new[]
    {
        DeviceType.Android,
        DeviceType.iOS,
        DeviceType.AndroidAmazon,
        DeviceType.LinuxDesktop,
        DeviceType.MacOsDesktop,
        DeviceType.WindowsDesktop,
        DeviceType.UWP,
    };

    public static IReadOnlyCollection<DeviceType> Types => _deviceTypes;
}
