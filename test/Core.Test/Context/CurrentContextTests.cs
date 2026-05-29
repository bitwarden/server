using System.Security.Claims;
using Bit.Core.AdminConsole.Context;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Identity;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Context;

[SutProviderCustomize]
public class CurrentContextTests
{
    #region BuildAsync(HttpContext) Tests

    [Theory, BitAutoData]
    public async Task BuildAsync_HttpContext_SetsHttpContext(
        SutProvider<CurrentContext> sutProvider)
    {
        var httpContext = new DefaultHttpContext();
        var globalSettings = new Core.Settings.GlobalSettings();
        // Act
        await sutProvider.Sut.BuildAsync(httpContext, globalSettings);

        // Assert
        Assert.Equal(httpContext, sutProvider.Sut.HttpContext);
    }

    [Theory, BitAutoData]
    public async Task BuildAsync_HttpContext_OnlyBuildsOnce(
        SutProvider<CurrentContext> sutProvider)
    {
        var httpContext = new DefaultHttpContext();
        var globalSettings = new Core.Settings.GlobalSettings();
        // Arrange
        await sutProvider.Sut.BuildAsync(httpContext, globalSettings);
        var firstContext = sutProvider.Sut.HttpContext;

        var secondHttpContext = new DefaultHttpContext();

        // Act
        await sutProvider.Sut.BuildAsync(secondHttpContext, globalSettings);

        // Assert
        Assert.Equal(firstContext, sutProvider.Sut.HttpContext);
        Assert.NotEqual(secondHttpContext, sutProvider.Sut.HttpContext);
    }

    [Theory, BitAutoData]
    public async Task BuildAsync_HttpContext_SetsDeviceIdentifier(
        SutProvider<CurrentContext> sutProvider,
        string expectedValue)
    {
        var httpContext = new DefaultHttpContext();
        var globalSettings = new Core.Settings.GlobalSettings();
        sutProvider.Sut.DeviceIdentifier = null;
        // Arrange
        httpContext.Request.Headers["Device-Identifier"] = expectedValue;

        // Act
        await sutProvider.Sut.BuildAsync(httpContext, globalSettings);

        // Assert
        Assert.Equal(expectedValue, sutProvider.Sut.DeviceIdentifier);
    }

    [Theory, BitAutoData]
    public async Task BuildAsync_HttpContext_SetsCountryName(
        SutProvider<CurrentContext> sutProvider,
        string countryName)
    {
        var httpContext = new DefaultHttpContext();
        var globalSettings = new Core.Settings.GlobalSettings();
        // Arrange
        httpContext.Request.Headers["country-name"] = countryName;

        // Act
        await sutProvider.Sut.BuildAsync(httpContext, globalSettings);

        // Assert
        Assert.Equal(countryName, sutProvider.Sut.CountryName);
    }

    [Theory, BitAutoData]
    public async Task BuildAsync_HttpContext_SetsDeviceType(
        SutProvider<CurrentContext> sutProvider)
    {
        var httpContext = new DefaultHttpContext();
        var globalSettings = new Core.Settings.GlobalSettings();
        // Arrange
        var deviceType = DeviceType.Android;
        httpContext.Request.Headers["Device-Type"] = ((int)deviceType).ToString();

        // Act
        await sutProvider.Sut.BuildAsync(httpContext, globalSettings);

        // Assert
        Assert.Equal(deviceType, sutProvider.Sut.DeviceType);
    }

    [Theory, BitAutoData]
    public async Task BuildAsync_HttpContext_SetsClientVersion(
        SutProvider<CurrentContext> sutProvider)
    {
        var httpContext = new DefaultHttpContext();
        var globalSettings = new Core.Settings.GlobalSettings();
        // Arrange
        var version = "2024.1.0";
        httpContext.Request.Headers["Bitwarden-Client-Version"] = version;
        httpContext.Request.Headers["Is-Prerelease"] = "1";

        // Act
        await sutProvider.Sut.BuildAsync(httpContext, globalSettings);

        // Assert
        Assert.Equal(new Version(version), sutProvider.Sut.ClientVersion);
        Assert.True(sutProvider.Sut.ClientVersionIsPrerelease);
    }

    #endregion

    #region SetContextAsync Tests

    [Theory, BitAutoData]
    public async Task SetContextAsync_NullUser_DoesNotThrow(
        SutProvider<CurrentContext> sutProvider)
    {
        // Act & Assert
        await sutProvider.Sut.SetContextAsync(null);
        // Should not throw
    }

    [Theory, BitAutoData]
    public async Task SetContextAsync_UserWithNoClaims_DoesNotThrow(
        SutProvider<CurrentContext> sutProvider)
    {
        // Arrange
        var user = new ClaimsPrincipal();

        // Act & Assert
        await sutProvider.Sut.SetContextAsync(user);
        // Should not throw
    }

    [Theory, BitAutoData]
    public async Task SetContextAsync_SendClient_ShortCircuits(
        SutProvider<CurrentContext> sutProvider,
        Guid userId)
    {
        // Arrange
        sutProvider.Sut.UserId = null;
        var claims = new List<Claim>
        {
            new(Claims.Type, IdentityClientType.Send.ToString()),
            new("sub", userId.ToString())
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        await sutProvider.Sut.SetContextAsync(user);

        // Assert
        Assert.Equal(IdentityClientType.Send, sutProvider.Sut.IdentityClientType);
        Assert.Null(sutProvider.Sut.UserId); // Should not be set for Send clients
    }

    [Theory, BitAutoData]
    public async Task SetContextAsync_RegularUser_SetsUserId(
        SutProvider<CurrentContext> sutProvider,
        Guid userId,
        string clientId)
    {
        // Arrange
        var claims = new List<Claim>
        {
            new("sub", userId.ToString()),
            new("client_id", clientId)
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        await sutProvider.Sut.SetContextAsync(user);

        // Assert
        Assert.Equal(userId, sutProvider.Sut.UserId);
        Assert.Equal(clientId, sutProvider.Sut.ClientId);
    }

    [Theory, BitAutoData]
    public async Task SetContextAsync_InstallationClient_SetsInstallationId(
        SutProvider<CurrentContext> sutProvider,
        Guid installationId)
    {
        // Arrange
        var claims = new List<Claim>
        {
            new("client_id", "installation.12345"),
            new("client_sub", installationId.ToString())
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        await sutProvider.Sut.SetContextAsync(user);

        // Assert
        Assert.Equal(installationId, sutProvider.Sut.InstallationId);
    }

    [Theory, BitAutoData]
    public async Task SetContextAsync_OrganizationClient_SetsOrganizationId(
        SutProvider<CurrentContext> sutProvider,
        Guid organizationId)
    {
        // Arrange
        var claims = new List<Claim>
        {
            new("client_id", "organization.12345"),
            new("client_sub", organizationId.ToString())
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        await sutProvider.Sut.SetContextAsync(user);

        // Assert
        Assert.Equal(organizationId, sutProvider.Sut.OrganizationId);
    }

    [Theory, BitAutoData]
    public async Task SetContextAsync_ServiceAccount_SetsServiceAccountOrganizationId(
        SutProvider<CurrentContext> sutProvider,
        Guid organizationId)
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(Claims.Type, IdentityClientType.ServiceAccount.ToString()),
            new(Claims.Organization, organizationId.ToString())
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        await sutProvider.Sut.SetContextAsync(user);

        // Assert
        Assert.Equal(IdentityClientType.ServiceAccount, sutProvider.Sut.IdentityClientType);
        Assert.Equal(organizationId, sutProvider.Sut.ServiceAccountOrganizationId);
    }

    [Theory, BitAutoData]
    public async Task SetContextAsync_WithDeviceClaims_SetsDeviceInfo(
        SutProvider<CurrentContext> sutProvider,
        string deviceIdentifier)
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(Claims.Device, deviceIdentifier),
            new(Claims.DeviceType, ((int)DeviceType.iOS).ToString())
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        await sutProvider.Sut.SetContextAsync(user);

        // Assert
        Assert.Equal(deviceIdentifier, sutProvider.Sut.DeviceIdentifier);
        Assert.Equal(DeviceType.iOS, sutProvider.Sut.DeviceType);
    }

    #endregion

    #region Organization Claims Tests

    [Theory]
    [BitAutoData(Claims.OrganizationOwner, OrganizationUserType.Owner)]
    [BitAutoData(Claims.OrganizationAdmin, OrganizationUserType.Admin)]
    [BitAutoData(Claims.OrganizationUser, OrganizationUserType.User)]
    public async Task SetContextAsync_OrganizationClaims_SetsOrganizations(
        string userOrgAssociation,
        OrganizationUserType userType,
        SutProvider<CurrentContext> sutProvider,
        Guid org1Id,
        Guid org2Id)
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(userOrgAssociation, org1Id.ToString()),
            new(userOrgAssociation, org2Id.ToString()),
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        await sutProvider.Sut.SetContextAsync(user);

        // Assert
        Assert.Equal(2, sutProvider.Sut.Organizations.Count);
        Assert.All(sutProvider.Sut.Organizations, org => Assert.Equal(userType, org.Type));
        Assert.Contains(sutProvider.Sut.Organizations, org => org.Id == org1Id);
        Assert.Contains(sutProvider.Sut.Organizations, org => org.Id == org2Id);
    }

    [Theory, BitAutoData]
    public async Task SetContextAsync_OrganizationCustomClaims_SetsOrganizationsWithPermissions(
        SutProvider<CurrentContext> sutProvider,
        Guid orgId)
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(Claims.OrganizationCustom, orgId.ToString()),
            new("accesseventlogs", orgId.ToString()),
            new("manageusers", orgId.ToString())
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        await sutProvider.Sut.SetContextAsync(user);

        // Assert
        Assert.Single(sutProvider.Sut.Organizations);
        var org = sutProvider.Sut.Organizations.First();
        Assert.Equal(OrganizationUserType.Custom, org.Type);
        Assert.Equal(orgId, org.Id);
        Assert.True(org.Permissions.AccessEventLogs);
        Assert.True(org.Permissions.ManageUsers);
        Assert.False(org.Permissions.ManageGroups);
    }

    [Theory, BitAutoData]
    public async Task SetContextAsync_SecretsManagerAccess_SetsAccessSecretsManager(
        SutProvider<CurrentContext> sutProvider,
        Guid orgId)
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(Claims.OrganizationOwner, orgId.ToString()),
            new(Claims.SecretsManagerAccess, orgId.ToString())
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        await sutProvider.Sut.SetContextAsync(user);

        // Assert
        Assert.Single(sutProvider.Sut.Organizations);
        Assert.True(sutProvider.Sut.Organizations.First().AccessSecretsManager);
    }

    #endregion

    #region Provider Claims Tests

    [Theory, BitAutoData]
    public async Task SetContextAsync_ProviderAdminClaims_SetsProviders(
        SutProvider<CurrentContext> sutProvider,
        Guid providerId)
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(Claims.ProviderAdmin, providerId.ToString())
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        await sutProvider.Sut.SetContextAsync(user);

        // Assert
        Assert.Single(sutProvider.Sut.Providers);
        Assert.Equal(ProviderUserType.ProviderAdmin, sutProvider.Sut.Providers.First().Type);
        Assert.Equal(providerId, sutProvider.Sut.Providers.First().Id);
    }

    [Theory, BitAutoData]
    public async Task SetContextAsync_ProviderServiceUserClaims_SetsProviders(
        SutProvider<CurrentContext> sutProvider,
        Guid providerId)
    {
        // Arrange
        var claims = new List<Claim>
        {
            new(Claims.ProviderServiceUser, providerId.ToString())
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims));

        // Act
        await sutProvider.Sut.SetContextAsync(user);

        // Assert
        Assert.Single(sutProvider.Sut.Providers);
        Assert.Equal(ProviderUserType.ServiceUser, sutProvider.Sut.Providers.First().Type);
        Assert.Equal(providerId, sutProvider.Sut.Providers.First().Id);
    }

    #endregion

    #region Organization Permission Tests

    [Theory, BitAutoData]
    public async Task OrganizationUser_WithDirectAccess_ReturnsTrue(
        SutProvider<CurrentContext> sutProvider,
        Guid orgId)
    {
        // Arrange
        sutProvider.Sut.Organizations = new List<CurrentContextOrganization>
        {
            new() { Id = orgId, Type = OrganizationUserType.User }
        };

        // Act
        var result = await sutProvider.Sut.OrganizationUser(orgId);

        // Assert
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task OrganizationUser_WithoutAccess_ReturnsFalse(
        SutProvider<CurrentContext> sutProvider,
        Guid orgId)
    {
        // Arrange
        sutProvider.Sut.Organizations = new List<CurrentContextOrganization>();

        // Act
        var result = await sutProvider.Sut.OrganizationUser(orgId);

        // Assert
        Assert.False(result);
    }

    [Theory, BitAutoData]
    public async Task OrganizationAdmin_WithAdminAccess_ReturnsTrue(
        SutProvider<CurrentContext> sutProvider,
        Guid orgId)
    {
        // Arrange
        sutProvider.Sut.Organizations = new List<CurrentContextOrganization>
        {
            new() { Id = orgId, Type = OrganizationUserType.Admin }
        };

        // Act
        var result = await sutProvider.Sut.OrganizationAdmin(orgId);

        // Assert
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task OrganizationOwner_WithOwnerAccess_ReturnsTrue(
        SutProvider<CurrentContext> sutProvider,
        Guid orgId)
    {
        // Arrange
        sutProvider.Sut.Organizations = new List<CurrentContextOrganization>
        {
            new() { Id = orgId, Type = OrganizationUserType.Owner }
        };

        // Act
        var result = await sutProvider.Sut.OrganizationOwner(orgId);

        // Assert
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task OrganizationCustom_WithCustomAccess_ReturnsTrue(
        SutProvider<CurrentContext> sutProvider,
        Guid orgId)
    {
        // Arrange
        sutProvider.Sut.Organizations = new List<CurrentContextOrganization>
        {
            new() { Id = orgId, Type = OrganizationUserType.Custom }
        };

        // Act
        var result = await sutProvider.Sut.OrganizationCustom(orgId);

        // Assert
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public async Task AccessEventLogs_WithPermission_ReturnsTrue(
        SutProvider<CurrentContext> sutProvider,
        Guid orgId)
    {
        // Arrange
        sutProvider.Sut.Organizations = new List<CurrentContextOrganization>
        {
            new()
            {
                Id = orgId,
                Type = OrganizationUserType.Custom,
                Permissions = new Permissions { AccessEventLogs = true }
            }
        };

        // Act
        var result = await sutProvider.Sut.AccessEventLogs(orgId);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region Provider Permission Tests

    [Theory, BitAutoData]
    public void ProviderProviderAdmin_WithAdminAccess_ReturnsTrue(
        SutProvider<CurrentContext> sutProvider,
        Guid providerId)
    {
        // Arrange
        sutProvider.Sut.Providers = new List<CurrentContextProvider>
        {
            new() { Id = providerId, Type = ProviderUserType.ProviderAdmin }
        };

        // Act
        var result = sutProvider.Sut.ProviderProviderAdmin(providerId);

        // Assert
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public void ProviderUser_WithAnyAccess_ReturnsTrue(
        SutProvider<CurrentContext> sutProvider,
        Guid providerId)
    {
        // Arrange
        sutProvider.Sut.Providers = new List<CurrentContextProvider>
        {
            new() { Id = providerId, Type = ProviderUserType.ServiceUser }
        };

        // Act
        var result = sutProvider.Sut.ProviderUser(providerId);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region Secrets Manager Tests

    [Theory, BitAutoData]
    public void AccessSecretsManager_WithServiceAccount_ReturnsTrue(
        SutProvider<CurrentContext> sutProvider,
        Guid orgId)
    {
        // Arrange
        sutProvider.Sut.ServiceAccountOrganizationId = orgId;

        // Act
        var result = sutProvider.Sut.AccessSecretsManager(orgId);

        // Assert
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public void AccessSecretsManager_WithOrgAccess_ReturnsTrue(
        SutProvider<CurrentContext> sutProvider,
        Guid orgId)
    {
        // Arrange
        sutProvider.Sut.Organizations = new List<CurrentContextOrganization>
        {
            new() { Id = orgId, AccessSecretsManager = true }
        };

        // Act
        var result = sutProvider.Sut.AccessSecretsManager(orgId);

        // Assert
        Assert.True(result);
    }

    [Theory, BitAutoData]
    public void AccessSecretsManager_WithoutAccess_ReturnsFalse(
        SutProvider<CurrentContext> sutProvider,
        Guid orgId)
    {
        // Arrange
        sutProvider.Sut.Organizations = new List<CurrentContextOrganization>
        {
            new() { Id = orgId, AccessSecretsManager = false }
        };

        // Act
        var result = sutProvider.Sut.AccessSecretsManager(orgId);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region Membership Loading Tests

    [Theory, BitAutoData]
    public async Task OrganizationMembershipAsync_LoadsFromRepository(
        SutProvider<CurrentContext> sutProvider,
        Guid userId,
        List<OrganizationUserOrganizationDetails> userOrgs)
    {
        // Arrange
        sutProvider.Sut.UserId = userId;
        sutProvider.Sut.Organizations = null;
        var organizationUserRepository = Substitute.For<IOrganizationUserRepository>();
        userOrgs.ForEach(org => org.Status = OrganizationUserStatusType.Confirmed);

        // Test complains about the JSON object that we store permissions as, so just set to empty object to pass the test.
        userOrgs.ForEach(org => org.Permissions = "{}");
        organizationUserRepository.GetManyDetailsByUserAsync(userId)
            .Returns(userOrgs);

        // Act
        var result = await sutProvider.Sut.OrganizationMembershipAsync(organizationUserRepository, userId);

        // Assert
        Assert.Equal(userOrgs.Count, result.Count);
        Assert.Equal(userId, sutProvider.Sut.UserId);
        await organizationUserRepository.Received(1).GetManyDetailsByUserAsync(userId);
    }

    [Theory, BitAutoData]
    public async Task ProviderMembershipAsync_LoadsFromRepository(
        SutProvider<CurrentContext> sutProvider,
        Guid userId,
        List<ProviderUser> userProviders)
    {
        // Arrange
        sutProvider.Sut.UserId = userId;
        sutProvider.Sut.Providers = null;

        var providerUserRepository = Substitute.For<IProviderUserRepository>();
        userProviders.ForEach(provider => provider.Status = ProviderUserStatusType.Confirmed);

        // Test complains about the JSON object that we store permissions as, so just set to empty object to pass the test.
        userProviders.ForEach(provider => provider.Permissions = "{}");
        providerUserRepository.GetManyByUserAsync(userId)
            .Returns(userProviders);

        // Act
        var result = await sutProvider.Sut.ProviderMembershipAsync(providerUserRepository, userId);

        // Assert
        Assert.Equal(userProviders.Count, result.Count);
        Assert.Equal(userId, sutProvider.Sut.UserId);
        await providerUserRepository.Received(1).GetManyByUserAsync(userId);
    }

    #endregion

    #region Utility Tests

    [Theory, BitAutoData]
    public void GetOrganization_WithExistingOrg_ReturnsOrganization(
        SutProvider<CurrentContext> sutProvider,
        Guid orgId)
    {
        // Arrange
        var org = new CurrentContextOrganization { Id = orgId };
        sutProvider.Sut.Organizations = new List<CurrentContextOrganization> { org };

        // Act
        var result = sutProvider.Sut.GetOrganization(orgId);

        // Assert
        Assert.Equal(org, result);
    }

    [Theory, BitAutoData]
    public void GetOrganization_WithNonExistingOrg_ReturnsNull(
        SutProvider<CurrentContext> sutProvider,
        Guid orgId)
    {
        // Arrange
        sutProvider.Sut.Organizations = new List<CurrentContextOrganization>();

        // Act
        var result = sutProvider.Sut.GetOrganization(orgId);

        // Assert
        Assert.Null(result);
    }

    #endregion
}
