using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Enums;

public enum DeviceType : byte
{
    [Display(Name = "Android")]
    Android = 0,

    [Display(Name = "iOS")]
    iOS = 1,

    [Display(Name = "Chrome Extension")]
    ChromeExtension = 2,

    [Display(Name = "Firefox Extension")]
    FirefoxExtension = 3,

    [Display(Name = "Opera Extension")]
    OperaExtension = 4,

    [Display(Name = "Edge Extension")]
    EdgeExtension = 5,

    [Display(Name = "Windows")]
    WindowsDesktop = 6,

    [Display(Name = "macOS")]
    MacOsDesktop = 7,

    [Display(Name = "Linux")]
    LinuxDesktop = 8,

    [Display(Name = "Chrome")]
    ChromeBrowser = 9,

    [Display(Name = "Firefox")]
    FirefoxBrowser = 10,

    [Display(Name = "Opera")]
    OperaBrowser = 11,

    [Display(Name = "Edge")]
    EdgeBrowser = 12,

    [Display(Name = "Internet Explorer")]
    IEBrowser = 13,

    [Display(Name = "Unknown Browser")]
    UnknownBrowser = 14,

    [Display(Name = "Android")]
    AndroidAmazon = 15,

    [Display(Name = "UWP")]
    UWP = 16,

    [Display(Name = "Safari")]
    SafariBrowser = 17,

    [Display(Name = "Vivaldi")]
    VivaldiBrowser = 18,

    [Display(Name = "Vivaldi Extension")]
    VivaldiExtension = 19,

    [Display(Name = "Safari Extension")]
    SafariExtension = 20,

    [Display(Name = "SDK")]
    SDK = 21,

    [Display(Name = "Server")]
    Server = 22,

    [Display(Name = "Windows CLI")]
    WindowsCLI = 23,

    [Display(Name = "MacOs CLI")]
    MacOsCLI = 24,

    [Display(Name = "Linux CLI")]
    LinuxCLI = 25,
}
