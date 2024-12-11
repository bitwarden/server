using Bit.Core.AdminConsole.Entities;

namespace Bit.Scim.Groups.Interfaces;

public interface IGetGroupsListQuery
{
    Task<(IEnumerable<Group> groupList, int totalResults)> GetGroupsListAsync(
        Guid organizationId,
        string filter,
        int? count,
        int? startIndex
    );
}
