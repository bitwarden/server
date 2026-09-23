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

        if (!string.IsNullOrWhiteSpace(filter))
        {
            if (ScimFilterParser.Parse(filter, out var attribute, out var op, out var value))
            {
                Func<Group, string> selector = attribute switch
                {
                    "displayname" => g => g.Name,
                    "externalid" => g => g.ExternalId,
                    _ => null
                };

                if (selector != null)
                {
                    groupList = groups
                        .Where(g => ScimFilterParser.Matches(selector(g), op, value))
                        .ToList();
                    totalResults = groupList.Count;
                }
            }
        }
        else
        {
            groupList = groups.OrderBy(g => g.Name)
                .Skip(startIndex - 1)
                .Take(count)
                .ToList();
            totalResults = groups.Count;
        }

        return (groupList, totalResults);
    }
}
