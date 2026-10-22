using Bit.Core.Settings;
using Bit.Seeder.Extensions;
using NSubstitute;
using Xunit;

namespace Bit.SeederApi.IntegrationTest.Extensions;

public class SeederEnvironmentGuardExtensionsTests
{
    [Fact]
    public void ThrowIfSelfHosted_SelfHosted_ThrowsWithSubjectAndSetting()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => Settings(selfHosted: true).ThrowIfSelfHosted("Providers"));

        Assert.StartsWith("Providers cannot exist on a self-hosted instance", exception.Message);
        Assert.Contains("'globalSettings:selfHosted' is true", exception.Message);
    }

    [Fact]
    public void ThrowIfSelfHosted_Cloud_DoesNotThrow()
    {
        Settings(selfHosted: false).ThrowIfSelfHosted("Providers");
    }

    [Fact]
    public void ThrowIfNotSelfHosted_Cloud_ThrowsWithSubjectAndSetting()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => Settings(selfHosted: false).ThrowIfNotSelfHosted("Self-host licenses"));

        Assert.StartsWith("Self-host licenses requires a self-hosted instance", exception.Message);
        Assert.Contains("'globalSettings:selfHosted' is false", exception.Message);
    }

    [Fact]
    public void ThrowIfNotSelfHosted_SelfHosted_DoesNotThrow()
    {
        Settings(selfHosted: true).ThrowIfNotSelfHosted("Self-host licenses");
    }

    [Theory]
    [InlineData("cus_1", null)]
    [InlineData(null, "sub_1")]
    [InlineData("cus_1", "sub_1")]
    [InlineData("cus_1", "")]
    [InlineData("", "sub_1")]
    public void ThrowIfSelfHostedWithGatewayIds_SelfHostedWithAnyId_ThrowsForGatewayIds(
        string? gatewayCustomerId, string? gatewaySubscriptionId)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => Settings(selfHosted: true).ThrowIfSelfHostedWithGatewayIds(gatewayCustomerId, gatewaySubscriptionId));

        Assert.StartsWith("Stripe gateway IDs cannot exist on a self-hosted instance", exception.Message);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void ThrowIfSelfHostedWithGatewayIds_SelfHostedWithoutIds_DoesNotThrow(
        string? gatewayCustomerId, string? gatewaySubscriptionId)
    {
        Settings(selfHosted: true).ThrowIfSelfHostedWithGatewayIds(gatewayCustomerId, gatewaySubscriptionId);
    }

    [Theory]
    [InlineData("cus_1", null)]
    [InlineData(null, "sub_1")]
    [InlineData("cus_1", "sub_1")]
    public void ThrowIfSelfHostedWithGatewayIds_Cloud_DoesNotThrow(
        string? gatewayCustomerId, string? gatewaySubscriptionId)
    {
        Settings(selfHosted: false).ThrowIfSelfHostedWithGatewayIds(gatewayCustomerId, gatewaySubscriptionId);
    }

    private static IGlobalSettings Settings(bool selfHosted)
    {
        var globalSettings = Substitute.For<IGlobalSettings>();
        globalSettings.SelfHosted.Returns(selfHosted);
        return globalSettings;
    }
}
