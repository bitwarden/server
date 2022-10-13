using Bit.Scim.Models;

namespace Bit.Scim.Users.Interfaces;

public interface IGetUserQuery
{
    Task<ScimUserResponseModel> GetUserAsync(Guid organizationId, Guid id);
}
