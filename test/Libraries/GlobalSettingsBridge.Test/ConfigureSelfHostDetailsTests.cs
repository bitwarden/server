using Bitwarden.Server.Sdk.Environment.Setup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Bit.GlobalSettingsBridge.Test;

public class ConfigureSelfHostDetailsTests
{
    [Fact]
    public void Configure_WhenCloud_SetsNotSelfHosted()
    {
        var details = Build(new() { ["globalSettings:selfHosted"] = "false" });

        Assert.False(details.SelfHosted);
        Assert.Null(details.SelfHostFlavor);
    }

    [Fact]
    public void Configure_WhenSelfHostedAndLite_SetsLiteFlavor()
    {
        var details = Build(new()
        {
            ["globalSettings:selfHosted"] = "true",
            ["globalSettings:liteDeployment"] = "true",
        });

        Assert.True(details.SelfHosted);
        Assert.Equal("lite", details.SelfHostFlavor);
    }

    [Fact]
    public void Configure_WhenSelfHostedAndNotLite_SetsUnknownFlavor()
    {
        var details = Build(new()
        {
            ["globalSettings:selfHosted"] = "true",
            ["globalSettings:liteDeployment"] = "false",
        });

        Assert.True(details.SelfHosted);
        Assert.Equal("unknown", details.SelfHostFlavor);
    }

    private static SelfHostDetails Build(Dictionary<string, string?> configValues)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddSingleton<IConfiguration>(config);
        services.AddGlobalSettingsBridge();
        return services.BuildServiceProvider().GetRequiredService<IOptions<SelfHostDetails>>().Value;
    }
}
