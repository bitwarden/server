using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
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

    private static OrganizationUserUserDetails CreateOrgUserDetails(Guid orgId, string resetPasswordKey)
    {
        return new OrganizationUserUserDetails
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            UserId = Guid.NewGuid(),
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
}
