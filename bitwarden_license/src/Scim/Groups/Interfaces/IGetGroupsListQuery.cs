using Bit.Core.AdminConsole.Entities;
using Bit.Scim.Models;

namespace Bit.Scim.Groups.Interfaces;

public interface IGetGroupsListQuery
{
    Task<(IEnumerable<Group> groupList, int totalResults)> GetGroupsListAsync(Guid organizationId, GetGroupsQueryParamModel model);
}
