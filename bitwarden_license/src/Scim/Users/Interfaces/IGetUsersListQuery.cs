using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Scim.Users.Interfaces;

public interface IGetUsersListQuery
{
    Task<(IEnumerable<OrganizationUserUserDetails> userList, int totalResults)> GetUsersListAsync(
        Guid organizationId,
        GetUsersQueryParamModel userQueryParams
    );
}
