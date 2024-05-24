using System.ComponentModel.DataAnnotations;
using Bit.Api.Auth.Models.Request;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models;
using Bit.Core.Entities;
using Xunit;

namespace Bit.Api.Test.Auth.Models.Request;

public class TwoFactorDuoRequestModelTests
{
    #region User Duo Provider
    [Fact]
    public void ToUser_ShouldAddOrUpdateTwoFactorProvider_WhenExistingProviderDoesNotExist()
    {
        // Arrange
        var existingUser = new User();
        var model = new UpdateTwoFactorDuoRequestModel
        {
            ClientId = "clientId",
            ClientSecret = "clientSecret",
            IntegrationKey = "integrationKey",
            SecretKey = "secretKey",
            Host = "example.com"
        };

        // Act
        var result = model.ToUser(existingUser);

        // Assert
        // IKey and SKey should be the same as ClientId and ClientSecret
        Assert.True(result.GetTwoFactorProviders().ContainsKey(TwoFactorProviderType.Duo));
        Assert.Equal("clientId", result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].MetaData["ClientId"]);
        Assert.Equal("clientSecret", result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].MetaData["ClientSecret"]);
        Assert.Equal("clientId", result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].MetaData["IKey"]);
        Assert.Equal("clientSecret", result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].MetaData["SKey"]);
        Assert.Equal("example.com", result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].MetaData["Host"]);
        Assert.True(result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].Enabled);
    }

    [Fact]
    public void ToUser_ShouldUpdateTwoFactorProvider_WhenExistingProviderExists()
    {
        // Arrange
        var existingUser = new User();
        existingUser.SetTwoFactorProviders(new Dictionary<TwoFactorProviderType, TwoFactorProvider>
        {
            { TwoFactorProviderType.Duo, new TwoFactorProvider() }
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
        var result = model.ToUser(existingUser);

        // Assert
        // IKey and SKey should be the same as ClientId and ClientSecret
        Assert.True(result.GetTwoFactorProviders().ContainsKey(TwoFactorProviderType.Duo));
        Assert.Equal("newClientId", result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].MetaData["ClientId"]);
        Assert.Equal("newClientSecret", result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].MetaData["ClientSecret"]);
        Assert.Equal("newClientId", result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].MetaData["IKey"]);
        Assert.Equal("newClientSecret", result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].MetaData["SKey"]);
        Assert.Equal("newExample.com", result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].MetaData["Host"]);
        Assert.True(result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].Enabled);
    }

    [Fact]
    public void ToUser_DuoV2ParamsSync_WhenExistingProviderDoesNotExist()
    {
        // Arrange
        var existingUser = new User();
        var model = new UpdateTwoFactorDuoRequestModel
        {
            IntegrationKey = "integrationKey",
            SecretKey = "secretKey",
            Host = "example.com"
        };

        // Act
        var result = model.ToUser(existingUser);

        // Assert
        // IKey and SKey should be the same as ClientId and ClientSecret
        Assert.True(result.GetTwoFactorProviders().ContainsKey(TwoFactorProviderType.Duo));
        Assert.Equal("integrationKey", result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].MetaData["ClientId"]);
        Assert.Equal("secretKey", result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].MetaData["ClientSecret"]);
        Assert.Equal("integrationKey", result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].MetaData["IKey"]);
        Assert.Equal("secretKey", result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].MetaData["SKey"]);
        Assert.Equal("example.com", result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].MetaData["Host"]);
        Assert.True(result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].Enabled);
    }

    [Fact]
    public void ToUser_DuoV4ParamsSync_WhenExistingProviderDoesNotExist()
    {
        // Arrange
        var existingUser = new User();
        var model = new UpdateTwoFactorDuoRequestModel
        {
            ClientId = "clientId",
            ClientSecret = "clientSecret",
            Host = "example.com"
        };

        // Act
        var result = model.ToUser(existingUser);

        // Assert
        // IKey and SKey should be the same as ClientId and ClientSecret
        Assert.True(result.GetTwoFactorProviders().ContainsKey(TwoFactorProviderType.Duo));
        Assert.Equal("clientId", result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].MetaData["ClientId"]);
        Assert.Equal("clientSecret", result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].MetaData["ClientSecret"]);
        Assert.Equal("clientId", result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].MetaData["IKey"]);
        Assert.Equal("clientSecret", result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].MetaData["SKey"]);
        Assert.Equal("example.com", result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].MetaData["Host"]);
        Assert.True(result.GetTwoFactorProviders()[TwoFactorProviderType.Duo].Enabled);
    }
    #endregion

    #region Organization Duo Provider
    [Fact]
    public void ToOrganization_ShouldAddOrUpdateTwoFactorProvider_WhenExistingProviderDoesNotExist()
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
    public void ToOrganization_ShouldUpdateTwoFactorProvider_WhenExistingProviderExists()
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
    public void ToOrganization_DuoV2ParamsSync_WhenExistingProviderDoesNotExist()
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
    public void ToOrganization_DuoV4ParamsSync_WhenExistingProviderDoesNotExist()
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
    #endregion

    [Fact]
    public void Validate_ShouldReturnValidationError_WhenHostIsInvalid()
    {
        // Arrange
        var model = new UpdateTwoFactorDuoRequestModel
        {
            Host = "invalidHost",
            ClientId = "clientId",
            ClientSecret = "clientSecret",
        };

        // Act
        var result = model.Validate(new ValidationContext(model));

        // Assert
        Assert.Single(result);
        Assert.Equal("Host is invalid.", result.First().ErrorMessage);
        Assert.Equal("Host", result.First().MemberNames.First());
    }

    [Fact]
    public void Validate_ShouldReturnValidationError_WhenValuesAreInvalid()
    {
        // Arrange
        var model = new UpdateTwoFactorDuoRequestModel
        {
            Host = "api-12345abc.duosecurity.com"
        };

        // Act
        var result = model.Validate(new ValidationContext(model));

        // Assert
        Assert.Single(result);
        Assert.Equal("Neither v2 or v4 values are valid.", result.First().ErrorMessage);
        Assert.Contains("ClientId", result.First().MemberNames);
        Assert.Contains("ClientSecret", result.First().MemberNames);
        Assert.Contains("IntegrationKey", result.First().MemberNames);
        Assert.Contains("SecretKey", result.First().MemberNames);
    }
}
