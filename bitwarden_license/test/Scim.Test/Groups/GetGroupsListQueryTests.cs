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
    [BitAutoData]
    public async Task GetGroupsList_FilterDisplayName_Ne_Success(SutProvider<GetGroupsListQuery> sutProvider, Guid organizationId, IList<Group> groups)
    {
        groups = SetGroupsOrganizationId(groups, organizationId);
        string name = groups.First().Name;
        string filter = $"displayName ne \"{name}\"";

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(groups);

        var result = await sutProvider.Sut.GetGroupsListAsync(organizationId, new GetGroupsQueryParamModel { Filter = filter });

        Assert.Equal(groups.Count - 1, result.totalResults);
        Assert.DoesNotContain(result.groupList, g => g.Name == name);
    }

    [Theory]
    [BitAutoData]
    public async Task GetGroupsList_FilterDisplayName_Co_Success(SutProvider<GetGroupsListQuery> sutProvider, Guid organizationId, IList<Group> groups)
    {
        groups = SetGroupsOrganizationId(groups, organizationId);
        groups.First().Name = "Test-Contains-Group";
        string filter = "displayName co \"Contains\"";

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(groups);

        var result = await sutProvider.Sut.GetGroupsListAsync(organizationId, new GetGroupsQueryParamModel { Filter = filter });

        Assert.Single(result.groupList);
        Assert.Equal("Test-Contains-Group", result.groupList.First().Name);
    }

    [Theory]
    [BitAutoData]
    public async Task GetGroupsList_FilterDisplayName_Sw_Success(SutProvider<GetGroupsListQuery> sutProvider, Guid organizationId, IList<Group> groups)
    {
        groups = SetGroupsOrganizationId(groups, organizationId);
        groups.First().Name = "Prefix-Test-Group";
        string filter = "displayName sw \"Prefix-\"";

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(groups);

        var result = await sutProvider.Sut.GetGroupsListAsync(organizationId, new GetGroupsQueryParamModel { Filter = filter });

        Assert.Single(result.groupList);
        Assert.Equal("Prefix-Test-Group", result.groupList.First().Name);
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
