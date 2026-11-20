using Bit.Sso.IntegrationTest.Helpers;
using Bitwarden.Server.Sdk.Environment;
using Xunit;

namespace Bit.Sso.IntegrationTest;

public class BitwardenEnvironmentTests
{
    [Fact]
    public async Task SelfHosted_GlobalSettingsSelfHostedTrue_IsTrue()
    {
        await using var factory = new SsoApplicationFactory();
        factory.UpdateConfiguration("globalSettings:selfHosted", "true");
        factory.UpdateConfiguration("globalSettings:installation:id", "10000000-0000-0000-0000-000000000000");

        var environment = factory.GetService<IBitwardenEnvironment>();

        Assert.True(environment.SelfHosted);
    }

    [Fact]
    public async Task SelfHosted_DefaultConfiguration_IsFalse()
    {
        await using var factory = new SsoApplicationFactory();

        var environment = factory.GetService<IBitwardenEnvironment>();

        Assert.False(environment.SelfHosted);
    }
}
