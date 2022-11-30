using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Scim.Groups;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Bit.Test.Common.Helpers;
using NSubstitute;
using Xunit;

namespace Bit.Scim.Test.Groups;

[SutProviderCustomize]
public class GetGroupsListCommandTests
{
    [Theory]
    [BitAutoData(10, 1)]
    [BitAutoData(2, 1)]
    [BitAutoData(1, 3)]
    public async Task GetGroupsList_Success(int count, int startIndex, SutProvider<GetGroupsListQuery> sutProvider, Guid organizationId, IList<Group> groups)
    {
        groups = SetGroupsOrganizationId(groups, organizationId);

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(groups);

        var result = await sutProvider.Sut.GetGroupsListAsync(organizationId, null, count, startIndex);

        AssertHelper.AssertPropertyEqual(groups.Skip(startIndex - 1).Take(count).ToList(), result.groupList);
        AssertHelper.AssertPropertyEqual(groups.Count, result.totalResults);
    }

    [Theory]
    [BitAutoData]
    public async Task GetGroupsList_FilterDisplayName_Success(SutProvider<GetGroupsListQuery> sutProvider, Guid organizationId, IList<Group> groups)
    {
        groups = SetGroupsOrganizationId(groups, organizationId);
        string name = groups.First().Name;
        string filter = $"displayName eq {name}";

        var expectedGroupList = groups
            .Where(g => g.Name == name)
            .ToList();
        var expectedTotalResults = expectedGroupList.Count;

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(groups);

        var result = await sutProvider.Sut.GetGroupsListAsync(organizationId, filter, null, null);

        AssertHelper.AssertPropertyEqual(expectedGroupList, result.groupList);
        AssertHelper.AssertPropertyEqual(expectedTotalResults, result.totalResults);
    }

    [Theory]
    [BitAutoData]
    public async Task GetGroupsList_FilterDisplayName_Empty(string name, SutProvider<GetGroupsListQuery> sutProvider, Guid organizationId, IList<Group> groups)
    {
        groups = SetGroupsOrganizationId(groups, organizationId);
        string filter = $"displayName eq {name}";

        var expectedGroupList = new List<Group>();
        var expectedTotalResults = expectedGroupList.Count;

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(groups);

        var result = await sutProvider.Sut.GetGroupsListAsync(organizationId, filter, null, null);

        AssertHelper.AssertPropertyEqual(expectedGroupList, result.groupList);
        AssertHelper.AssertPropertyEqual(expectedTotalResults, result.totalResults);
    }

    [Theory]
    [BitAutoData]
    public async Task GetGroupsList_FilterExternalId_Success(SutProvider<GetGroupsListQuery> sutProvider, Guid organizationId, IList<Group> groups)
    {
        groups = SetGroupsOrganizationId(groups, organizationId);
        string externalId = groups.First().ExternalId;
        string filter = $"externalId eq {externalId}";

        var expectedGroupList = groups
            .Where(ou => ou.ExternalId == externalId)
            .ToList();
        var expectedTotalResults = expectedGroupList.Count;

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(groups);

        var result = await sutProvider.Sut.GetGroupsListAsync(organizationId, filter, null, null);

        AssertHelper.AssertPropertyEqual(expectedGroupList, result.groupList);
        AssertHelper.AssertPropertyEqual(expectedTotalResults, result.totalResults);
    }

    [Theory]
    [BitAutoData]
    public async Task GetGroupsList_FilterExternalId_Empty(string externalId, SutProvider<GetGroupsListQuery> sutProvider, Guid organizationId, IList<Group> groups)
    {
        groups = SetGroupsOrganizationId(groups, organizationId);
        string filter = $"externalId eq {externalId}";

        var expectedGroupList = groups
            .Where(ou => ou.ExternalId == externalId)
            .ToList();
        var expectedTotalResults = expectedGroupList.Count;

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(groups);

        var result = await sutProvider.Sut.GetGroupsListAsync(organizationId, filter, null, null);

        AssertHelper.AssertPropertyEqual(expectedGroupList, result.groupList);
        AssertHelper.AssertPropertyEqual(expectedTotalResults, result.totalResults);
    }

    private IList<Group> SetGroupsOrganizationId(IList<Group> groups, Guid organizationId)
    {
        return groups.Select(g =>
        {
            g.OrganizationId = organizationId;
            return g;
        }).ToList();
    }
}
