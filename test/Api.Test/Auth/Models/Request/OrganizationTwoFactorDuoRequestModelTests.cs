using Bit.Api.Auth.Models.Request;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Xunit;

namespace Bit.Api.Test.Auth.Models.Request;

public class OrganizationTwoFactorDuoRequestModelTests
{

    [Fact]
    public void ShouldAddOrUpdateTwoFactorProvider_WhenExistingProviderDoesNotExist()
    {
        // Arrange
        var existingOrg = new Organization();
        var model = new UpdateTwoFactorDuoRequestModel
        {
            ClientId = "clientId",
            ClientSecret = "clientSecret",
            IntegrationKey = "integrationKey",
            SecretKey = "secretKey",
            Host = "example.com"
        };

        // Act
        var result = model.ToOrganization(existingOrg);

        // Assert
        Assert.True(result.GetTwoFactorProviders().ContainsKey(TwoFactorProviderType.OrganizationDuo));
        Assert.Equal("clientId", result.GetTwoFactorProviders()[TwoFactorProviderType.OrganizationDuo].MetaData["ClientId"]);
        Assert.Equal("clientSecret", result.GetTwoFactorProviders()[TwoFactorProviderType.OrganizationDuo].MetaData["ClientSecret"]);
        Assert.Equal("clientId", result.GetTwoFactorProviders()[TwoFactorProviderType.OrganizationDuo].MetaData["IKey"]);
        Assert.Equal("clientSecret", result.GetTwoFactorProviders()[TwoFactorProviderType.OrganizationDuo].MetaData["SKey"]);
        Assert.Equal("example.com", result.GetTwoFactorProviders()[TwoFactorProviderType.OrganizationDuo].MetaData["Host"]);
        Assert.True(result.GetTwoFactorProviders()[TwoFactorProviderType.OrganizationDuo].Enabled);
    }

    [Fact]
    public void ShouldUpdateTwoFactorProvider_WhenExistingProviderExists()
    {
        // Arrange
        var existingOrg = new Organization();
        existingOrg.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            { TwoFactorProviderType.OrganizationDuo, new TwoFactorProvider() }
        });
        var model = new UpdateTwoFactorDuoRequestModel
        {
            ClientId = "newClientId",
            ClientSecret = "newClientSecret",
            IntegrationKey = "newIntegrationKey",
            SecretKey = "newSecretKey",
            Host = "newExample.com"
        };

        // Act
        var result = model.ToOrganization(existingOrg);

        // Assert
        Assert.True(result.GetTwoFactorProviders().ContainsKey(TwoFactorProviderType.OrganizationDuo));
        Assert.Equal("newClientId", result.GetTwoFactorProviders()[TwoFactorProviderType.OrganizationDuo].MetaData["ClientId"]);
        Assert.Equal("newClientSecret", result.GetTwoFactorProviders()[TwoFactorProviderType.OrganizationDuo].MetaData["ClientSecret"]);
        Assert.Equal("newClientId", result.GetTwoFactorProviders()[TwoFactorProviderType.OrganizationDuo].MetaData["IKey"]);
        Assert.Equal("newClientSecret", result.GetTwoFactorProviders()[TwoFactorProviderType.OrganizationDuo].MetaData["SKey"]);
        Assert.Equal("newExample.com", result.GetTwoFactorProviders()[TwoFactorProviderType.OrganizationDuo].MetaData["Host"]);
        Assert.True(result.GetTwoFactorProviders()[TwoFactorProviderType.OrganizationDuo].Enabled);
    }

    [Fact]
    public void DuoV2ParamsSync_WhenExistingProviderDoesNotExist()
    {
        // Arrange
        var existingOrg = new Organization();
        var model = new UpdateTwoFactorDuoRequestModel
        {
            IntegrationKey = "integrationKey",
            SecretKey = "secretKey",
            Host = "example.com"
        };

        // Act
        var result = model.ToOrganization(existingOrg);

        // Assert
        // IKey and SKey should be the same as ClientId and ClientSecret
        Assert.True(result.GetTwoFactorProviders().ContainsKey(TwoFactorProviderType.OrganizationDuo));
        Assert.Equal("integrationKey", result.GetTwoFactorProviders()[TwoFactorProviderType.OrganizationDuo].MetaData["ClientId"]);
        Assert.Equal("secretKey", result.GetTwoFactorProviders()[TwoFactorProviderType.OrganizationDuo].MetaData["ClientSecret"]);
        Assert.Equal("integrationKey", result.GetTwoFactorProviders()[TwoFactorProviderType.OrganizationDuo].MetaData["IKey"]);
        Assert.Equal("secretKey", result.GetTwoFactorProviders()[TwoFactorProviderType.OrganizationDuo].MetaData["SKey"]);
        Assert.Equal("example.com", result.GetTwoFactorProviders()[TwoFactorProviderType.OrganizationDuo].MetaData["Host"]);
        Assert.True(result.GetTwoFactorProviders()[TwoFactorProviderType.OrganizationDuo].Enabled);
    }

    [Fact]
    public void DuoV4ParamsSync_WhenExistingProviderDoesNotExist()
    {
        // Arrange
        var existingOrg = new Organization();
        var model = new UpdateTwoFactorDuoRequestModel
        {
            ClientId = "clientId",
            ClientSecret = "clientSecret",
            Host = "example.com"
        };

        // Act
        var result = model.ToOrganization(existingOrg);

        // Assert
        // IKey and SKey should be the same as ClientId and ClientSecret
        Assert.True(result.GetTwoFactorProviders().ContainsKey(TwoFactorProviderType.OrganizationDuo));
        Assert.Equal("clientId", result.GetTwoFactorProviders()[TwoFactorProviderType.OrganizationDuo].MetaData["ClientId"]);
        Assert.Equal("clientSecret", result.GetTwoFactorProviders()[TwoFactorProviderType.OrganizationDuo].MetaData["ClientSecret"]);
        Assert.Equal("clientId", result.GetTwoFactorProviders()[TwoFactorProviderType.OrganizationDuo].MetaData["IKey"]);
        Assert.Equal("clientSecret", result.GetTwoFactorProviders()[TwoFactorProviderType.OrganizationDuo].MetaData["SKey"]);
        Assert.Equal("example.com", result.GetTwoFactorProviders()[TwoFactorProviderType.OrganizationDuo].MetaData["Host"]);
        Assert.True(result.GetTwoFactorProviders()[TwoFactorProviderType.OrganizationDuo].Enabled);
    }
}
