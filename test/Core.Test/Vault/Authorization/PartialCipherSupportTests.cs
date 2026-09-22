using Bit.Core.Enums;
using Bit.Core.Vault.Authorization;
using Xunit;

namespace Bit.Core.Test.Vault.Authorization;

public class PartialCipherSupportTests
{
    [Theory]
    [InlineData(DeviceType.ChromeBrowser, false)]
    [InlineData(DeviceType.ChromeBrowser, true)]
    [InlineData(DeviceType.FirefoxBrowser, false)]
    [InlineData(DeviceType.SafariBrowser, false)]
    [InlineData(DeviceType.EdgeBrowser, false)]
    [InlineData(DeviceType.UnknownBrowser, false)]
    public void IsSupportedBy_WebVault_IsSupportedWhateverTheFlag(DeviceType deviceType, bool browserExtensionsEnabled)
    {
        Assert.True(PartialCipherSupport.IsSupportedBy(deviceType, browserExtensionsEnabled));
    }

    [Theory]
    [InlineData(DeviceType.ChromeExtension)]
    [InlineData(DeviceType.FirefoxExtension)]
    [InlineData(DeviceType.SafariExtension)]
    [InlineData(DeviceType.EdgeExtension)]
    [InlineData(DeviceType.OperaExtension)]
    [InlineData(DeviceType.VivaldiExtension)]
    public void IsSupportedBy_BrowserExtension_FlagOff_IsNotSupported(DeviceType deviceType)
    {
        Assert.False(PartialCipherSupport.IsSupportedBy(deviceType, browserExtensionsEnabled: false));
    }

    [Theory]
    [InlineData(DeviceType.ChromeExtension)]
    [InlineData(DeviceType.FirefoxExtension)]
    [InlineData(DeviceType.SafariExtension)]
    [InlineData(DeviceType.EdgeExtension)]
    [InlineData(DeviceType.OperaExtension)]
    [InlineData(DeviceType.VivaldiExtension)]
    public void IsSupportedBy_BrowserExtension_FlagOn_IsSupported(DeviceType deviceType)
    {
        Assert.True(PartialCipherSupport.IsSupportedBy(deviceType, browserExtensionsEnabled: true));
    }

    [Theory]
    [InlineData(DeviceType.WindowsDesktop)]
    [InlineData(DeviceType.MacOsDesktop)]
    [InlineData(DeviceType.LinuxDesktop)]
    [InlineData(DeviceType.Android)]
    [InlineData(DeviceType.iOS)]
    [InlineData(DeviceType.WindowsCLI)]
    [InlineData(DeviceType.MacOsCLI)]
    [InlineData(DeviceType.LinuxCLI)]
    public void IsSupportedBy_OtherClients_IsNotSupportedEvenWithTheFlag(DeviceType deviceType)
    {
        Assert.False(PartialCipherSupport.IsSupportedBy(deviceType, browserExtensionsEnabled: true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsSupportedBy_NoDeviceType_IsNotSupported(bool browserExtensionsEnabled)
    {
        Assert.False(PartialCipherSupport.IsSupportedBy(null, browserExtensionsEnabled));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IsSupportedBy_UnrecognizedDeviceType_IsNotSupported(bool browserExtensionsEnabled)
    {
        Assert.False(PartialCipherSupport.IsSupportedBy((DeviceType)byte.MaxValue, browserExtensionsEnabled));
    }
}
