using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Scim.Users.Interfaces;

public interface IGetUserQuery
{
    Task<OrganizationUserUserDetails> GetUserAsync(Guid organizationId, Guid id);
}
