using Bit.Scim.Models;

namespace Bit.Scim.Queries.Groups.Interfaces;

public interface IGetGroupsListQuery
{
    Task<ScimListResponseModel<ScimGroupResponseModel>> GetGroupsListAsync(Guid organizationId, string filter, int? count, int? startIndex);
}
