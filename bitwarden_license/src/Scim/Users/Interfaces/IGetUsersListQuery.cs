using Bit.Scim.Models;

namespace Bit.Scim.Users.Interfaces;

public interface IGetUsersListQuery
{
    Task<ScimListResponseModel<ScimUserResponseModel>> GetUsersListAsync(Guid organizationId, string filter, int? count, int? startIndex);
}
