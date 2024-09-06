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
        DeviceType.WindowsCLI,
        DeviceType.MacOsCLI,
        DeviceType.LinuxCLI,
    };

    public static IReadOnlyCollection<DeviceType> BrowserExtensionTypes { get; } = new[]
    {
        DeviceType.ChromeExtension,
        DeviceType.FirefoxExtension,
        DeviceType.OperaExtension,
        DeviceType.EdgeExtension,
        DeviceType.VivaldiExtension,
        DeviceType.SafariExtension
    };

    public static IReadOnlyCollection<DeviceType> BrowserTypes { get; } = new[]
    {
        DeviceType.ChromeBrowser,
        DeviceType.FirefoxBrowser,
        DeviceType.OperaBrowser,
        DeviceType.EdgeBrowser,
        DeviceType.IEBrowser,
        DeviceType.UnknownBrowser,
        DeviceType.SafariBrowser,
        DeviceType.VivaldiBrowser
    };
}
