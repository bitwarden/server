using Bit.Core.Enums;

namespace Bit.Core.Utilities;

public static class DeviceTypes
{
    public static IReadOnlyCollection<DeviceType> MobileTypes { get; } = new[]
    {
        DeviceType.Android,
        DeviceType.iOS,
        DeviceType.AndroidAmazon,
    };

    public static IReadOnlyCollection<DeviceType> DesktopTypes { get; } = new[]
    {
        DeviceType.LinuxDesktop,
        DeviceType.MacOsDesktop,
        DeviceType.WindowsDesktop,
        DeviceType.UWP,
    };
}
