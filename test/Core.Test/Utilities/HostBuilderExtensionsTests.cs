using Bit.Core.Utilities;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Bit.Core.Test.Utilities;

public class HostBuilderExtensionsTests
{
    [Fact]
    public void ConfigureCustomAppConfiguration_ShouldRegisterKeyPerFile()
    {
        // Arrange
        var globalSettingsSelfHostedBeforeTest = Environment.GetEnvironmentVariable("globalSettings__selfHosted");
        Environment.SetEnvironmentVariable("globalSettings__selfHosted", "true");
        try
        {
            var hostBuilder = new HostBuilder();
            hostBuilder.ConfigureAppConfiguration((context, _) => context.Configuration["globalSettings:installation:fileConfigDirectory"] = Path.GetTempPath());

            // Act
            hostBuilder.ConfigureCustomAppConfiguration([]);

            // Assert
            hostBuilder.ConfigureAppConfiguration(builder => { Assert.Contains(builder.Sources, source => source.GetType().Name == "KeyPerFileConfigurationSource"); });
        }
        finally
        {
            Environment.SetEnvironmentVariable("globalSettings__selfHosted", globalSettingsSelfHostedBeforeTest);
        }
    }
}
