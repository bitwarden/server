using Bit.Scim.Models;

namespace Bit.Scim.Queries.Groups.Interfaces;

public interface IGetGroupQuery
{
    Task<ScimGroupResponseModel> GetGroupAsync(Guid organizationId, Guid groupId);
}
