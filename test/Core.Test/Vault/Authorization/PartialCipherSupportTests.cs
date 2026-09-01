using Bit.Core.Enums;
using Bit.Core.Vault.Authorization;
using Xunit;

namespace Bit.Core.Test.Vault.Authorization;

public class PartialCipherSupportTests
{
    [Theory]
    [InlineData(DeviceType.ChromeBrowser)]
    [InlineData(DeviceType.FirefoxBrowser)]
    [InlineData(DeviceType.SafariBrowser)]
    [InlineData(DeviceType.EdgeBrowser)]
    [InlineData(DeviceType.UnknownBrowser)]
    public void IsSupportedBy_WebVault_IsSupported(DeviceType deviceType)
    {
        Assert.True(PartialCipherSupport.IsSupportedBy(deviceType));
    }

    [Theory]
    // Browser extensions are a distinct client from the web vault and do not understand the shape.
    [InlineData(DeviceType.ChromeExtension)]
    [InlineData(DeviceType.FirefoxExtension)]
    [InlineData(DeviceType.SafariExtension)]
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
        Assert.False(PartialCipherSupport.IsSupportedBy(deviceType));
    }

    [Fact]
    public void IsSupportedBy_NoDeviceType_IsNotSupported()
    {
        // Fails safe: an unidentified caller must not be sent a shape it may not understand.
        Assert.False(PartialCipherSupport.IsSupportedBy(null));
    }

    [Fact]
    public void IsSupportedBy_UnrecognizedDeviceType_IsNotSupported()
    {
        Assert.False(PartialCipherSupport.IsSupportedBy((DeviceType)byte.MaxValue));
    }
}
