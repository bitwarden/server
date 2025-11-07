using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.Services;

public interface IOrganizationUserUserDetailsCache
{
    Task<OrganizationUserUserDetails?> GetAsync(Guid organizationId, Guid userId);
}
