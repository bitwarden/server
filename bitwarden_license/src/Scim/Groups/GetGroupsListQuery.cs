using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Scim.Groups.Interfaces;

namespace Bit.Scim.Groups;

public class GetGroupsListQuery : IGetGroupsListQuery
{
    private readonly IGroupRepository _groupRepository;

    public GetGroupsListQuery(IGroupRepository groupRepository)
    {
        _groupRepository = groupRepository;
    }

    public async Task<(IEnumerable<Group> groupList, int totalResults)> GetGroupsListAsync(Guid organizationId, string filter, int? count, int? startIndex)
    {
        string nameFilter = null;
        string externalIdFilter = null;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            if (filter.StartsWith("displayName eq "))
            {
                nameFilter = filter.Substring(15).Trim('"');
            }
            else if (filter.StartsWith("externalId eq "))
            {
                externalIdFilter = filter.Substring(14).Trim('"');
            }
        }

        var groupList = new List<Group>();
        var groups = await _groupRepository.GetManyByOrganizationIdAsync(organizationId);
        var totalResults = 0;
        if (!string.IsNullOrWhiteSpace(nameFilter))
        {
            var group = groups.FirstOrDefault(g => g.Name == nameFilter);
            if (group != null)
            {
                groupList.Add(group);
            }
            totalResults = groupList.Count;
        }
        else if (!string.IsNullOrWhiteSpace(externalIdFilter))
        {
            var group = groups.FirstOrDefault(ou => ou.ExternalId == externalIdFilter);
            if (group != null)
            {
                groupList.Add(group);
            }
            totalResults = groupList.Count;
        }
        else if (string.IsNullOrWhiteSpace(filter) && startIndex.HasValue && count.HasValue)
        {
            groupList = groups.OrderBy(g => g.Name)
                .Skip(startIndex.Value - 1)
                .Take(count.Value)
                .ToList();
            totalResults = groups.Count;
        }

        return (groupList, totalResults);
    }
}
