using Bit.Core.Settings;
using Bit.Seeder.Extensions;
using NSubstitute;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Extensions;

public class SeederEnvironmentGuardExtensionsTests
{
    [Fact]
    public void ThrowIfSelfHosted_SelfHosted_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => GlobalSettings(selfHosted: true).ThrowIfSelfHosted("Providers"));

        Assert.StartsWith("Providers", exception.Message);
        Assert.Contains("globalSettings:selfHosted", exception.Message);
    }

    [Fact]
    public void ThrowIfSelfHosted_Cloud_DoesNotThrow()
    {
        GlobalSettings(selfHosted: false).ThrowIfSelfHosted("Providers");
    }

    [Fact]
    public void ThrowIfNotSelfHosted_Cloud_Throws()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => GlobalSettings(selfHosted: false).ThrowIfNotSelfHosted("A SelfHosted premium user"));

        Assert.StartsWith("A SelfHosted premium user", exception.Message);
        Assert.Contains("cloud mode", exception.Message);
        Assert.Contains("globalSettings:selfHosted", exception.Message);
    }

    [Fact]
    public void ThrowIfNotSelfHosted_SelfHosted_DoesNotThrow()
    {
        GlobalSettings(selfHosted: true).ThrowIfNotSelfHosted("A SelfHosted premium user");
    }

    private static IGlobalSettings GlobalSettings(bool selfHosted)
    {
        var globalSettings = Substitute.For<IGlobalSettings>();
        globalSettings.SelfHosted.Returns(selfHosted);
        return globalSettings;
    }
}
