using Bit.Api.AdminConsole.Authorization.Requirements;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Api.Test.AdminConsole.Authorization.Requirements;

[SutProviderCustomize]
public class ManageGroupsOrUsersRequirementTests
{
    [Theory]
    [CurrentContextOrganizationCustomize]
    [BitAutoData(OrganizationUserType.Admin)]
    [BitAutoData(OrganizationUserType.Owner)]
    public async Task AuthorizeAsync_WhenUserTypeCanManageUsers_ThenRequestShouldBeAuthorized(
        OrganizationUserType type,
        CurrentContextOrganization organization,
        SutProvider<ManageGroupsOrUsersRequirement> sutProvider)
    {
        organization.Type = type;

        var actual = await sutProvider.Sut.AuthorizeAsync(organization, () => Task.FromResult(false));

        Assert.True(actual);
    }

    [Theory]
    [CurrentContextOrganizationCustomize]
    [BitAutoData(OrganizationUserType.Custom, true, false)]
    [BitAutoData(OrganizationUserType.Custom, false, true)]
    public async Task AuthorizeAsync_WhenCustomUserThatCanManageUsersOrGroups_ThenRequestShouldBeAuthorized(
        OrganizationUserType type,
        bool canManageUsers,
        bool canManageGroups,
        CurrentContextOrganization organization,
        SutProvider<ManageGroupsOrUsersRequirement> sutProvider)
    {
        organization.Type = type;
        organization.Permissions = new Permissions { ManageUsers = canManageUsers, ManageGroups = canManageGroups };

        var actual = await sutProvider.Sut.AuthorizeAsync(organization, () => Task.FromResult(false));

        Assert.True(actual);
    }

    [Theory]
    [CurrentContextOrganizationCustomize]
    [BitAutoData]
    public async Task AuthorizeAsync_WhenProviderUserForAnOrganization_ThenRequestShouldBeAuthorized(
        CurrentContextOrganization organization,
        SutProvider<ManageGroupsOrUsersRequirement> sutProvider)
    {
        var actual = await sutProvider.Sut.AuthorizeAsync(organization, IsProviderUserForOrg);

        Assert.True(actual);
        return;

        Task<bool> IsProviderUserForOrg() => Task.FromResult(true);
    }

    [Theory]
    [CurrentContextOrganizationCustomize]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    public async Task AuthorizeAsync_WhenUserCannotManageUsersOrGroupsAndIsNotAProviderUser_ThenRequestShouldBeDenied(
        OrganizationUserType type,
        CurrentContextOrganization organization,
        SutProvider<ManageGroupsOrUsersRequirement> sutProvider)
    {
        organization.Type = type;
        organization.Permissions = new Permissions { ManageUsers = false, ManageGroups = false }; // When Type is User, the canManage permissions don't matter

        var actual = await sutProvider.Sut.AuthorizeAsync(organization, IsNotProviderUserForOrg);

        Assert.False(actual);
        return;

        Task<bool> IsNotProviderUserForOrg() => Task.FromResult(false);
    }
}
