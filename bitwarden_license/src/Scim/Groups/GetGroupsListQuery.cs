// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Scim.Groups.Interfaces;
using Bit.Scim.Models;
using Bit.Scim.Utilities;

namespace Bit.Scim.Groups;

public class GetGroupsListQuery : IGetGroupsListQuery
{
    private readonly IGroupRepository _groupRepository;

    public GetGroupsListQuery(IGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }

    public async Task<(IEnumerable<Group> groupList, int totalResults)> GetGroupsListAsync(
        Guid organizationId, GetGroupsQueryParamModel groupQueryParams)
    {
        int count = groupQueryParams.Count;
        int startIndex = groupQueryParams.StartIndex;
        string filter = groupQueryParams.Filter;

        var groups = await _groupRepository.GetManyByOrganizationIdAsync(organizationId);
        var groupList = new List<Group>();
        var totalResults = 0;

        var predicate = ScimFilterParser.TryGetPredicate<Group>(filter,
            new Dictionary<string, Func<Group, string>>
            {
                ["displayname"] = g => g.Name,
                ["externalid"] = g => g.ExternalId
            });

        IEnumerable<Group> filtered;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            filtered = predicate != null
                ? groups.Where(predicate)
                : Enumerable.Empty<Group>();
        }
        else
        {
            filtered = groups.AsEnumerable();
        }

        totalResults = filtered.Count();
        groupList = filtered.OrderBy(g => g.Name)
            .Skip(startIndex - 1)
            .Take(count)
            .ToList();

        return (groupList, totalResults);
    }
}
