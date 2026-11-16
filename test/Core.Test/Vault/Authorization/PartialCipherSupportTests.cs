using Bit.Core.Enums;
using Bit.Core.Vault.Authorization;
using Xunit;

namespace Bit.Core.Test.Vault.Authorization;

public class PartialCipherSupportTests
{
    private static readonly Version _minimumVersion = new(Constants.PamPartialCipherMinimumVersion);
    private static readonly Version _newerVersion = new(_minimumVersion.Major, _minimumVersion.Minor + 1, 0);
    private static readonly Version _olderVersion =
        new(_minimumVersion.Major, _minimumVersion.Minor, _minimumVersion.Build - 1);

    [Theory]
    [InlineData(DeviceType.ChromeBrowser, false)]
    [InlineData(DeviceType.FirefoxBrowser, false)]
    [InlineData(DeviceType.SafariBrowser, false)]
    [InlineData(DeviceType.EdgeBrowser, false)]
    [InlineData(DeviceType.UnknownBrowser, false)]
    [InlineData(DeviceType.ChromeBrowser, true)]
    public void IsSupportedBy_WebVault_MinimumVersion_IsSupportedRegardlessOfFlag(
        DeviceType deviceType, bool browserExtensionsEnabled)
    {
        Assert.True(PartialCipherSupport.IsSupportedBy(deviceType, _minimumVersion, browserExtensionsEnabled));
    }

    [Fact]
    public void IsSupportedBy_WebVault_NewerVersion_IsSupported()
    {
        Assert.True(PartialCipherSupport.IsSupportedBy(
            DeviceType.ChromeBrowser, _newerVersion, browserExtensionsEnabled: false));
    }

    [Fact]
    public void IsSupportedBy_WebVault_OlderVersion_IsNotSupported()
    {
        Assert.False(PartialCipherSupport.IsSupportedBy(
            DeviceType.ChromeBrowser, _olderVersion, browserExtensionsEnabled: true));
    }

    [Fact]
    public void IsSupportedBy_WebVault_NoVersion_IsNotSupported()
    {
        Assert.False(PartialCipherSupport.IsSupportedBy(
            DeviceType.ChromeBrowser, clientVersion: null, browserExtensionsEnabled: true));
    }

    [Theory]
    [InlineData(DeviceType.ChromeExtension)]
    [InlineData(DeviceType.FirefoxExtension)]
    [InlineData(DeviceType.OperaExtension)]
    [InlineData(DeviceType.EdgeExtension)]
    [InlineData(DeviceType.VivaldiExtension)]
    [InlineData(DeviceType.SafariExtension)]
    [InlineData(DeviceType.DuckDuckGoExtension)]
    public void IsSupportedBy_BrowserExtension_FlagOnAndMinimumVersion_IsSupported(DeviceType deviceType)
    {
        Assert.True(PartialCipherSupport.IsSupportedBy(deviceType, _minimumVersion, browserExtensionsEnabled: true));
    }

    [Fact]
    public void IsSupportedBy_BrowserExtension_FlagOnAndNewerVersion_IsSupported()
    {
        Assert.True(PartialCipherSupport.IsSupportedBy(
            DeviceType.ChromeExtension, _newerVersion, browserExtensionsEnabled: true));
    }

    [Fact]
    public void IsSupportedBy_BrowserExtension_FlagOnButOlderVersion_IsNotSupported()
    {
        Assert.False(PartialCipherSupport.IsSupportedBy(
            DeviceType.ChromeExtension, _olderVersion, browserExtensionsEnabled: true));
    }

    [Fact]
    public void IsSupportedBy_BrowserExtension_FlagOnButNoVersion_IsNotSupported()
    {
        Assert.False(PartialCipherSupport.IsSupportedBy(
            DeviceType.ChromeExtension, clientVersion: null, browserExtensionsEnabled: true));
    }

    [Theory]
    [InlineData(DeviceType.ChromeExtension)]
    [InlineData(DeviceType.FirefoxExtension)]
    [InlineData(DeviceType.SafariExtension)]
    public void IsSupportedBy_BrowserExtension_FlagOff_IsNotSupported(DeviceType deviceType)
    {
        Assert.False(PartialCipherSupport.IsSupportedBy(deviceType, _newerVersion, browserExtensionsEnabled: false));
    }

    [Theory]
    // Desktop
    [InlineData(DeviceType.WindowsDesktop)]
    [InlineData(DeviceType.MacOsDesktop)]
    [InlineData(DeviceType.LinuxDesktop)]
    // Mobile
    [InlineData(DeviceType.Android)]
    [InlineData(DeviceType.iOS)]
    // CLI
    [InlineData(DeviceType.WindowsCLI)]
    [InlineData(DeviceType.MacOsCLI)]
    [InlineData(DeviceType.LinuxCLI)]
    public void IsSupportedBy_OtherClients_IsNotSupported(DeviceType deviceType)
    {
        Assert.False(PartialCipherSupport.IsSupportedBy(deviceType, _newerVersion, browserExtensionsEnabled: true));
    }

    [Fact]
    public void IsSupportedBy_NoDeviceType_IsNotSupported()
    {
        Assert.False(PartialCipherSupport.IsSupportedBy(null, _newerVersion, browserExtensionsEnabled: true));
    }

    [Fact]
    public void IsSupportedBy_UnrecognizedDeviceType_IsNotSupported()
    {
        Assert.False(PartialCipherSupport.IsSupportedBy(
            (DeviceType)byte.MaxValue, _newerVersion, browserExtensionsEnabled: true));
    }
}
