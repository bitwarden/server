using Bit.Commercial.Core.SecretsManager.Queries.AccessPolicies;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Commercial.Core.Test.SecretsManager.Queries.AccessPolicies;

[SutProviderCustomize]
public class SameOrganizationQueryTests
{
    [Theory]
    [BitAutoData]
    public async Task OrgUsersInTheSameOrg_NoOrgUsers_ReturnsFalse(SutProvider<SameOrganizationQuery> sutProvider,
        List<OrganizationUser> orgUsers, Guid organizationId)
    {
        var orgUserIds = orgUsers.Select(ou => ou.Id).ToList();
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(orgUserIds)
            .ReturnsForAnyArgs(new List<OrganizationUser>());

        var result = await sutProvider.Sut.OrgUsersInTheSameOrgAsync(orgUserIds, organizationId);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData]
    public async Task OrgUsersInTheSameOrg_OrgMismatch_ReturnsFalse(SutProvider<SameOrganizationQuery> sutProvider,
        List<OrganizationUser> orgUsers, Guid organizationId)
    {
        var orgUserIds = orgUsers.Select(ou => ou.Id).ToList();
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(orgUserIds)
            .ReturnsForAnyArgs(orgUsers);

        var result = await sutProvider.Sut.OrgUsersInTheSameOrgAsync(orgUserIds, organizationId);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData]
    public async Task OrgUsersInTheSameOrg_CountMismatch_ReturnsFalse(SutProvider<SameOrganizationQuery> sutProvider,
        List<OrganizationUser> orgUsers, Guid organizationId)
    {
        var orgUserIds = orgUsers.Select(ou => ou.Id).ToList();
        foreach (var organizationUser in orgUsers)
        {
            organizationUser.OrganizationId = organizationId;
        }

        orgUsers.RemoveAt(0);
        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(orgUserIds)
            .ReturnsForAnyArgs(orgUsers);

        var result = await sutProvider.Sut.OrgUsersInTheSameOrgAsync(orgUserIds, organizationId);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData]
    public async Task OrgUsersInTheSameOrg_Success_ReturnsTrue(SutProvider<SameOrganizationQuery> sutProvider,
        List<OrganizationUser> orgUsers, Guid organizationId)
    {
        var orgUserIds = orgUsers.Select(ou => ou.Id).ToList();
        foreach (var organizationUser in orgUsers)
        {
            organizationUser.OrganizationId = organizationId;
        }

        sutProvider.GetDependency<IOrganizationUserRepository>().GetManyAsync(orgUserIds)
            .ReturnsForAnyArgs(orgUsers);

        var result = await sutProvider.Sut.OrgUsersInTheSameOrgAsync(orgUserIds, organizationId);

        Assert.True(result);
    }

    [Theory]
    [BitAutoData]
    public async Task GroupsInTheSameOrg_NoGroups_ReturnsFalse(SutProvider<SameOrganizationQuery> sutProvider,
        List<Group> groups, Guid organizationId)
    {
        var groupIds = groups.Select(ou => ou.Id).ToList();
        sutProvider.GetDependency<IGroupRepository>().GetManyByManyIds(groupIds)
            .ReturnsForAnyArgs(new List<Group>());

        var result = await sutProvider.Sut.GroupsInTheSameOrgAsync(groupIds, organizationId);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData]
    public async Task GroupsInTheSameOrg_OrgMismatch_ReturnsFalse(SutProvider<SameOrganizationQuery> sutProvider,
        List<Group> groups, Guid organizationId)
    {
        var groupIds = groups.Select(ou => ou.Id).ToList();
        sutProvider.GetDependency<IGroupRepository>().GetManyByManyIds(groupIds)
            .ReturnsForAnyArgs(groups);

        var result = await sutProvider.Sut.GroupsInTheSameOrgAsync(groupIds, organizationId);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData]
    public async Task GroupsInTheSameOrg_CountMismatch_ReturnsFalse(SutProvider<SameOrganizationQuery> sutProvider,
        List<Group> groups, Guid organizationId)
    {
        var groupIds = groups.Select(ou => ou.Id).ToList();
        foreach (var group in groups)
        {
            group.OrganizationId = organizationId;
        }

        groups.RemoveAt(0);

        sutProvider.GetDependency<IGroupRepository>().GetManyByManyIds(groupIds)
            .ReturnsForAnyArgs(groups);

        var result = await sutProvider.Sut.GroupsInTheSameOrgAsync(groupIds, organizationId);

        Assert.False(result);
    }

    [Theory]
    [BitAutoData]
    public async Task GroupsInTheSameOrg_Success_ReturnsTrue(SutProvider<SameOrganizationQuery> sutProvider,
        List<Group> groups, Guid organizationId)
    {
        var groupIds = groups.Select(ou => ou.Id).ToList();
        foreach (var group in groups)
        {
            group.OrganizationId = organizationId;
        }

        sutProvider.GetDependency<IGroupRepository>().GetManyByManyIds(groupIds)
            .ReturnsForAnyArgs(groups);


        var result = await sutProvider.Sut.GroupsInTheSameOrgAsync(groupIds, organizationId);

        Assert.True(result);
    }
}
