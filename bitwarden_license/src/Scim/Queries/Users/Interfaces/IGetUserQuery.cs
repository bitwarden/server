using Bit.Scim.Models;

namespace Bit.Scim.Queries.Users.Interfaces;

public interface IGetUserQuery
{
    Task<ScimUserResponseModel> GetUserAsync(Guid organizationId, Guid id);
}
