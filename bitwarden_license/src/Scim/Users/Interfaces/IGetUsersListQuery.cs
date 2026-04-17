using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Scim.Models;

namespace Bit.Scim.Users.Interfaces;

public interface IGetUsersListQuery
{
    Task<(IEnumerable<OrganizationUserUserDetails> userList, int totalResults)> GetUsersListAsync(Guid organizationId, GetUsersQueryParamModel userQueryParams);
}
