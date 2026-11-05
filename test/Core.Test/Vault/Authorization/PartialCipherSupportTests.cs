using Bit.Core.Enums;
using Bit.Core.Vault.Authorization;
using Xunit;

namespace Bit.Core.Test.Vault.Authorization;

public class PartialCipherSupportTests
{
    [Theory]
    [InlineData(DeviceType.ChromeBrowser, false)]
    [InlineData(DeviceType.FirefoxBrowser, false)]
    [InlineData(DeviceType.SafariBrowser, false)]
    [InlineData(DeviceType.EdgeBrowser, false)]
    [InlineData(DeviceType.UnknownBrowser, false)]
    [InlineData(DeviceType.ChromeBrowser, true)]
    public void IsSupportedBy_WebVault_IsSupported(DeviceType deviceType, bool browserExtensionsEnabled)
    {
        Assert.True(PartialCipherSupport.IsSupportedBy(deviceType, browserExtensionsEnabled));
    }

    [Theory]
    [InlineData(DeviceType.ChromeExtension)]
    [InlineData(DeviceType.FirefoxExtension)]
    [InlineData(DeviceType.OperaExtension)]
    [InlineData(DeviceType.EdgeExtension)]
    [InlineData(DeviceType.VivaldiExtension)]
    [InlineData(DeviceType.SafariExtension)]
    [InlineData(DeviceType.DuckDuckGoExtension)]
    public void IsSupportedBy_BrowserExtension_FlagOn_IsSupported(DeviceType deviceType)
    {
        Assert.True(PartialCipherSupport.IsSupportedBy(deviceType, browserExtensionsEnabled: true));
    }

    [Theory]
    [InlineData(DeviceType.ChromeExtension)]
    [InlineData(DeviceType.FirefoxExtension)]
    [InlineData(DeviceType.SafariExtension)]
    public void IsSupportedBy_BrowserExtension_FlagOff_IsNotSupported(DeviceType deviceType)
    {
        Assert.False(PartialCipherSupport.IsSupportedBy(deviceType, browserExtensionsEnabled: false));
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
        // The flag is for browser extensions only.
        Assert.False(PartialCipherSupport.IsSupportedBy(deviceType, browserExtensionsEnabled: true));
    }

    [Fact]
    public void IsSupportedBy_NoDeviceType_IsNotSupported()
    {
        // Fails safe: an unidentified caller must not be sent a shape it may not understand.
        Assert.False(PartialCipherSupport.IsSupportedBy(null, browserExtensionsEnabled: true));
    }

    [Fact]
    public void IsSupportedBy_UnrecognizedDeviceType_IsNotSupported()
    {
        Assert.False(PartialCipherSupport.IsSupportedBy((DeviceType)byte.MaxValue, browserExtensionsEnabled: true));
    }
}
