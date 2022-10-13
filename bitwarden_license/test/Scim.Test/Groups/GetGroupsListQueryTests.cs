using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Scim.Groups;
using Bit.Scim.Models;
using Bit.Scim.Utilities;
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
    public async Task GetGroupsList_Success(int? count, int? startIndex, SutProvider<GetGroupsListQuery> sutProvider, Guid organizationId, IList<Group> groups)
    {
        groups = SetGroupsOrganizationId(groups, organizationId);

        var expectedResult = new ScimListResponseModel<ScimGroupResponseModel>
        {
            Resources = groups
                .OrderBy(g => g.Name)
                .Skip(startIndex.Value - 1)
                .Take(count.Value)
                .Select(g => new Models.ScimGroupResponseModel(g))
                .ToList(),
            ItemsPerPage = count.GetValueOrDefault(groups.Count),
            TotalResults = groups.Count,
            StartIndex = startIndex.GetValueOrDefault(1),
            Schemas = new List<string> { ScimConstants.Scim2SchemaListResponse }
        };

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(groups);

        var result = await sutProvider.Sut.GetGroupsListAsync(organizationId, null, count, startIndex);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).GetManyByOrganizationIdAsync(organizationId);

        AssertHelper.AssertPropertyEqual(expectedResult, result);
    }

    [Theory]
    [BitAutoData]
    public async Task GetGroupsList_FilterDisplayName_Success(SutProvider<GetGroupsListQuery> sutProvider, Guid organizationId, IList<Group> groups)
    {
        groups = SetGroupsOrganizationId(groups, organizationId);
        string name = groups.First().Name;
        string filter = $"displayName eq {name}";

        var expectedResult = new ScimListResponseModel<ScimGroupResponseModel>
        {
            Resources = groups
                .Where(g => g.Name == name)
                .Select(g => new Models.ScimGroupResponseModel(g))
                .ToList(),
            ItemsPerPage = 1,
            TotalResults = 1,
            StartIndex = 1,
            Schemas = new List<string> { ScimConstants.Scim2SchemaListResponse }
        };

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(groups);

        var result = await sutProvider.Sut.GetGroupsListAsync(organizationId, filter, null, null);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).GetManyByOrganizationIdAsync(organizationId);

        AssertHelper.AssertPropertyEqual(expectedResult, result);
    }

    [Theory]
    [BitAutoData]
    public async Task GetGroupsList_FilterDisplayName_Empty(string name, SutProvider<GetGroupsListQuery> sutProvider, Guid organizationId, IList<Group> groups)
    {
        groups = SetGroupsOrganizationId(groups, organizationId);
        string filter = $"displayName eq {name}";

        var expectedResult = new ScimListResponseModel<ScimGroupResponseModel>
        {
            Resources = new List<ScimGroupResponseModel>(),
            ItemsPerPage = 0,
            TotalResults = 0,
            StartIndex = 1,
            Schemas = new List<string> { ScimConstants.Scim2SchemaListResponse }
        };

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(groups);

        var result = await sutProvider.Sut.GetGroupsListAsync(organizationId, filter, null, null);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).GetManyByOrganizationIdAsync(organizationId);

        AssertHelper.AssertPropertyEqual(expectedResult, result);
    }

    [Theory]
    [BitAutoData]
    public async Task GetGroupsList_FilterExternalId_Success(SutProvider<GetGroupsListQuery> sutProvider, Guid organizationId, IList<Group> groups)
    {
        groups = SetGroupsOrganizationId(groups, organizationId);
        string externalId = groups.First().ExternalId;
        string filter = $"externalId eq {externalId}";

        var expectedResult = new ScimListResponseModel<ScimGroupResponseModel>
        {
            Resources = groups
                .Where(ou => ou.ExternalId == externalId)
                .Select(g => new Models.ScimGroupResponseModel(g))
                .ToList(),
            ItemsPerPage = 1,
            TotalResults = 1,
            StartIndex = 1,
            Schemas = new List<string> { ScimConstants.Scim2SchemaListResponse }
        };

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(groups);

        var result = await sutProvider.Sut.GetGroupsListAsync(organizationId, filter, null, null);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).GetManyByOrganizationIdAsync(organizationId);

        AssertHelper.AssertPropertyEqual(expectedResult, result);
    }

    [Theory]
    [BitAutoData]
    public async Task GetGroupsList_FilterExternalId_Empty(string externalId, SutProvider<GetGroupsListQuery> sutProvider, Guid organizationId, IList<Group> groups)
    {
        groups = SetGroupsOrganizationId(groups, organizationId);
        string filter = $"externalId eq {externalId}";

        var expectedResult = new ScimListResponseModel<ScimGroupResponseModel>
        {
            Resources = new List<ScimGroupResponseModel>(),
            ItemsPerPage = 0,
            TotalResults = 0,
            StartIndex = 1,
            Schemas = new List<string> { ScimConstants.Scim2SchemaListResponse }
        };

        sutProvider.GetDependency<IGroupRepository>()
            .GetManyByOrganizationIdAsync(organizationId)
            .Returns(groups);

        var result = await sutProvider.Sut.GetGroupsListAsync(organizationId, filter, null, null);

        await sutProvider.GetDependency<IGroupRepository>().Received(1).GetManyByOrganizationIdAsync(organizationId);

        AssertHelper.AssertPropertyEqual(expectedResult, result);
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
