using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Scim.Groups;
using Bit.Scim.Models;
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

        var result = await sutProvider.Sut.GetGroupsListAsync(organizationId, new GetGroupsQueryParamModel { Count = count, StartIndex = startIndex });

        Assert.Equal(groups.Count, result.totalResults);
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

        var result = await sutProvider.Sut.GetGroupsListAsync(organizationId, new GetGroupsQueryParamModel { Filter = filter });

        Assert.Equal(expectedTotalResults, result.totalResults);
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

        var result = await sutProvider.Sut.GetGroupsListAsync(organizationId, new GetGroupsQueryParamModel { Filter = filter });

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

        var result = await sutProvider.Sut.GetGroupsListAsync(organizationId, new GetGroupsQueryParamModel { Filter = filter });

        Assert.Equal(expectedTotalResults, result.totalResults);
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

        var result = await sutProvider.Sut.GetGroupsListAsync(organizationId, new GetGroupsQueryParamModel { Filter = filter });

        Assert.Equal(expectedTotalResults, result.totalResults);
    }

    [Theory]
    [BitAutoData("active eq true")]
    [BitAutoData("displayName pr")]
    [BitAutoData("displayName eq \"Test\" and externalId eq \"abc\"")]
    [BitAutoData("displayName eq \"Test\" or externalId eq \"abc\"")]
    [BitAutoData("members[value eq \"abc\"]")]
    public async Task GetGroupsList_UnsupportedFilter_ThrowsInvalidFilter(string filter, SutProvider<GetGroupsListQuery> sutProvider, Guid organizationId)
    {
        var exception = await Assert.ThrowsAsync<ScimInvalidFilterException>(() =>
            sutProvider.Sut.GetGroupsListAsync(organizationId, new GetGroupsQueryParamModel { Filter = filter }));

        Assert.False(string.IsNullOrWhiteSpace(exception.Message));

        // The repository should never be queried for an unsupported filter.
        await sutProvider.GetDependency<IGroupRepository>()
            .DidNotReceive()
            .GetManyByOrganizationIdAsync(Arg.Any<Guid>());
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
