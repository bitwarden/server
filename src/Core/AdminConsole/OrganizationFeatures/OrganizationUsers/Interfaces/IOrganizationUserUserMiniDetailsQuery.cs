#nullable enable

using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IOrganizationUserUserMiniDetailsQuery
{
    public Task<IEnumerable<OrganizationUserUserMiniDetails>> Get(Guid orgId);
}
