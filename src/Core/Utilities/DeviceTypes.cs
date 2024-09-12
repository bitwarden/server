using Bit.Core.Enums;

namespace Bit.Core.Utilities;

public static class DeviceTypes
{
    public static IReadOnlyCollection<DeviceType> MobileTypes { get; } =
    [
        DeviceType.Android,
        DeviceType.iOS,
        DeviceType.AndroidAmazon
    ];

    public static IReadOnlyCollection<DeviceType> DesktopTypes { get; } =
    [
        DeviceType.LinuxDesktop,
        DeviceType.MacOsDesktop,
        DeviceType.WindowsDesktop,
        DeviceType.UWP,
        DeviceType.WindowsCLI,
        DeviceType.MacOsCLI,
        DeviceType.LinuxCLI
    ];


    public static IReadOnlyCollection<DeviceType> BrowserExtensionTypes { get; } =
    [
        DeviceType.ChromeExtension,
        DeviceType.FirefoxExtension,
        DeviceType.OperaExtension,
        DeviceType.EdgeExtension,
        DeviceType.VivaldiExtension,
        DeviceType.SafariExtension
    ];

    public static IReadOnlyCollection<DeviceType> BrowserTypes { get; } =
    [
        DeviceType.ChromeBrowser,
        DeviceType.FirefoxBrowser,
        DeviceType.OperaBrowser,
        DeviceType.EdgeBrowser,
        DeviceType.IEBrowser,
        DeviceType.SafariBrowser,
        DeviceType.VivaldiBrowser,
        DeviceType.UnknownBrowser
    ];
}
