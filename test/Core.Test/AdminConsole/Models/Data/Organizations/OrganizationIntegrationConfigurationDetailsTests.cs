using System.Text.Json;
using Bit.Core.Models.Data.Organizations;
using Xunit;

namespace Bit.Core.Test.Models.Data.Organizations;

public class OrganizationIntegrationConfigurationDetailsTests
{
    [Fact]
    public void MergedConfiguration_WithValidConfigAndIntegration_ReturnsMergedJson()
    {
        var config = new { config = "A new config value" };
        var integration = new { integration = "An integration value" };
        var expectedObj = new { integration = "An integration value", config = "A new config value" };
        var expected = JsonSerializer.Serialize(expectedObj);

        var sut = new OrganizationIntegrationConfigurationDetails();
        sut.Configuration = JsonSerializer.Serialize(config);
        sut.IntegrationConfiguration = JsonSerializer.Serialize(integration);

        var result = sut.MergedConfiguration;
        Assert.Equal(expected, result.ToJsonString());
    }

    [Fact]
    public void MergedConfiguration_WithSameKeyIndConfigAndIntegration_GivesPrecedenceToConfiguration()
    {
        var config = new { config = "A new config value" };
        var integration = new { config = "An integration value" };
        var expectedObj = new { config = "A new config value" };
        var expected = JsonSerializer.Serialize(expectedObj);

        var sut = new OrganizationIntegrationConfigurationDetails();
        sut.Configuration = JsonSerializer.Serialize(config);
        sut.IntegrationConfiguration = JsonSerializer.Serialize(integration);

        var result = sut.MergedConfiguration;
        Assert.Equal(expected, result.ToJsonString());
    }

    [Fact]
    public void MergedConfiguration_WithInvalidJsonConfigAndIntegration_ReturnsEmptyJson()
    {
        var expectedObj = new { };
        var expected = JsonSerializer.Serialize(expectedObj);

        var sut = new OrganizationIntegrationConfigurationDetails();
        sut.Configuration = "Not JSON";
        sut.IntegrationConfiguration = "Not JSON";

        var result = sut.MergedConfiguration;
        Assert.Equal(expected, result.ToJsonString());
    }

    [Fact]
    public void MergedConfiguration_WithNullConfigAndIntegration_ReturnsEmptyJson()
    {
        var expectedObj = new { };
        var expected = JsonSerializer.Serialize(expectedObj);

        var sut = new OrganizationIntegrationConfigurationDetails();
        sut.Configuration = null;
        sut.IntegrationConfiguration = null;

        var result = sut.MergedConfiguration;
        Assert.Equal(expected, result.ToJsonString());
    }

    [Fact]
    public void MergedConfiguration_WithValidIntegrationAndNullConfig_ReturnsIntegrationJson()
    {
        var integration = new { integration = "An integration value" };
        var expectedObj = new { integration = "An integration value" };
        var expected = JsonSerializer.Serialize(expectedObj);

        var sut = new OrganizationIntegrationConfigurationDetails();
        sut.Configuration = null;
        sut.IntegrationConfiguration = JsonSerializer.Serialize(integration);

        var result = sut.MergedConfiguration;
        Assert.Equal(expected, result.ToJsonString());
    }

    [Fact]
    public void MergedConfiguration_WithValidConfigAndNullIntegration_ReturnsConfigJson()
    {
        var config = new { config = "A new config value" };
        var expectedObj = new { config = "A new config value" };
        var expected = JsonSerializer.Serialize(expectedObj);

        var sut = new OrganizationIntegrationConfigurationDetails();
        sut.Configuration = JsonSerializer.Serialize(config);
        sut.IntegrationConfiguration = null;

        var result = sut.MergedConfiguration;
        Assert.Equal(expected, result.ToJsonString());
    }
}
