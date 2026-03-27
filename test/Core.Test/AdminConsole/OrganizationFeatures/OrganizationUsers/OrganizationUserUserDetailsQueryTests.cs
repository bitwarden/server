using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.UserFeatures.TwoFactorAuth.Interfaces;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers;
using Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Requests;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers;

[SutProviderCustomize]
public class OrganizationUserUserDetailsQueryTests
{
    [Theory]
    [BitAutoData(" ")]
    public async Task GetAccountRecoveryEnrolledUsers_InvalidKey_FiltersOut(
        string invalidKey,
        Guid orgId,
        SutProvider<OrganizationUserUserDetailsQuery> sutProvider)
    {
        // Arrange
        var request = new OrganizationUserUserDetailsQueryRequest { OrganizationId = orgId };

        var validUser = CreateOrgUserDetails(orgId, "valid-key");
        var invalidUser = CreateOrgUserDetails(orgId, invalidKey);
        var allUsers = new[] { validUser, invalidUser };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync_vNext(orgId, false, false)
            .Returns(allUsers);

        SetupTwoFactorAndClaimedStatus(sutProvider, orgId);

        // Act
        var result = (await sutProvider.Sut.GetAccountRecoveryEnrolledUsers(request)).ToList();

        // Assert - invalid key user should be filtered out
        Assert.Single(result);
        Assert.Equal(validUser.Id, result[0].OrgUser.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task GetAccountRecoveryEnrolledUsers_NullKey_FiltersOut(
        Guid orgId,
        SutProvider<OrganizationUserUserDetailsQuery> sutProvider)
    {
        // Arrange
        var request = new OrganizationUserUserDetailsQueryRequest { OrganizationId = orgId };

        var validUser = CreateOrgUserDetails(orgId, "valid-key");
        var nullUser = CreateOrgUserDetails(orgId, null!);
        var allUsers = new[] { validUser, nullUser };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync_vNext(orgId, false, false)
            .Returns(allUsers);

        SetupTwoFactorAndClaimedStatus(sutProvider, orgId);

        // Act
        var result = (await sutProvider.Sut.GetAccountRecoveryEnrolledUsers(request)).ToList();

        // Assert
        Assert.Single(result);
        Assert.Equal(validUser.Id, result[0].OrgUser.Id);
    }

    [Theory]
    [BitAutoData]
    public async Task Get_UserIsProviderUser_SetsIsProviderUserTrue(
        Guid orgId,
        Guid providerId,
        SutProvider<OrganizationUserUserDetailsQuery> sutProvider)
    {
        // Arrange
        var request = new OrganizationUserUserDetailsQueryRequest { OrganizationId = orgId };

        var providerUserId = Guid.NewGuid();
        var providerUser = CreateOrgUserDetails(orgId, "valid-key", providerUserId);
        var nonProviderUser = CreateOrgUserDetails(orgId, "valid-key");
        var allUsers = new[] { providerUser, nonProviderUser };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync_vNext(orgId, false, false)
            .Returns(allUsers);

        SetupTwoFactorAndClaimedStatus(sutProvider, orgId);
        SetupProviderOrg(sutProvider, orgId, providerId);
        SetupProviderUsers(sutProvider, providerId, [providerUserId]);

        // Act
        var result = (await sutProvider.Sut.Get(request)).ToList();

        // Assert
        Assert.True(result.Single(r => r.OrgUser.Id == providerUser.Id).OrgUser.IsProviderUser);
        Assert.False(result.Single(r => r.OrgUser.Id == nonProviderUser.Id).OrgUser.IsProviderUser);
    }

    [Theory]
    [BitAutoData]
    public async Task Get_UserIsNotProviderUser_SetsIsProviderUserFalse(
        Guid orgId,
        Guid providerId,
        SutProvider<OrganizationUserUserDetailsQuery> sutProvider)
    {
        // Arrange
        var request = new OrganizationUserUserDetailsQueryRequest { OrganizationId = orgId };

        var orgUser = CreateOrgUserDetails(orgId, "valid-key");
        var allUsers = new[] { orgUser };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync_vNext(orgId, false, false)
            .Returns(allUsers);

        SetupTwoFactorAndClaimedStatus(sutProvider, orgId);
        SetupProviderOrg(sutProvider, orgId, providerId);
        SetupProviderUsers(sutProvider, providerId, []);

        // Act
        var result = (await sutProvider.Sut.Get(request)).ToList();

        // Assert
        Assert.False(result.Single().OrgUser.IsProviderUser);
    }

    [Theory]
    [BitAutoData]
    public async Task Get_OrgHasNoProvider_IsProviderUserFalseForAll(
        Guid orgId,
        SutProvider<OrganizationUserUserDetailsQuery> sutProvider)
    {
        // Arrange
        var request = new OrganizationUserUserDetailsQueryRequest { OrganizationId = orgId };

        var orgUser = CreateOrgUserDetails(orgId, "valid-key");
        var allUsers = new[] { orgUser };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyDetailsByOrganizationAsync_vNext(orgId, false, false)
            .Returns(allUsers);

        SetupTwoFactorAndClaimedStatus(sutProvider, orgId);

        sutProvider.GetDependency<IProviderOrganizationRepository>()
            .GetByOrganizationId(orgId)
            .Returns((ProviderOrganization?)null);

        // Act
        var result = (await sutProvider.Sut.Get(request)).ToList();

        // Assert
        Assert.False(result.Single().OrgUser.IsProviderUser);
    }

    private static OrganizationUserUserDetails CreateOrgUserDetails(Guid orgId, string resetPasswordKey,
        Guid? userId = null)
    {
        return new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            UserId = userId ?? Guid.NewGuid(),
            Status = OrganizationUserStatusType.Confirmed,
            Type = OrganizationUserType.User,
            UsesKeyConnector = false,
            ResetPasswordKey = resetPasswordKey,
            Email = "test@example.com"
        };
    }

    private static void SetupTwoFactorAndClaimedStatus(
        SutProvider<OrganizationUserUserDetailsQuery> sutProvider, Guid orgId)
    {
        sutProvider.GetDependency<ITwoFactorIsEnabledQuery>()
            .TwoFactorIsEnabledAsync(Arg.Any<IEnumerable<OrganizationUserUserDetails>>())
            .Returns(callInfo =>
            {
                var users = callInfo.Arg<IEnumerable<OrganizationUserUserDetails>>();
                return users.Select(u => (user: u, twoFactorIsEnabled: false)).ToList();
            });

        sutProvider.GetDependency<IGetOrganizationUsersClaimedStatusQuery>()
            .GetUsersOrganizationClaimedStatusAsync(Arg.Any<Guid>(), Arg.Any<IEnumerable<Guid>>())
            .Returns(callInfo =>
            {
                var userIds = callInfo.Arg<IEnumerable<Guid>>();
                return userIds.ToDictionary(id => id, _ => false);
            });
    }

    private static void SetupProviderOrg(
        SutProvider<OrganizationUserUserDetailsQuery> sutProvider, Guid orgId, Guid providerId)
    {
        sutProvider.GetDependency<IProviderOrganizationRepository>()
            .GetByOrganizationId(orgId)
            .Returns(new ProviderOrganization { ProviderId = providerId, OrganizationId = orgId });
    }

    private static void SetupProviderUsers(
        SutProvider<OrganizationUserUserDetailsQuery> sutProvider, Guid providerId, IEnumerable<Guid> userIds)
    {
        var providerUserDetails = userIds
            .Select(uid => new ProviderUserUserDetails { UserId = uid })
            .ToList();

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyDetailsByProviderAsync(providerId)
            .Returns(providerUserDetails);
    }
}
