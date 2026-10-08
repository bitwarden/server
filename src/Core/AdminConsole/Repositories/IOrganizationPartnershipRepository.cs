using Bit.Core.AdminConsole.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.Repositories;

public interface IOrganizationPartnershipRepository : IRepository<OrganizationPartnership, Guid>
{
    Task<OrganizationPartnership?> GetByOrganizationIdAsync(Guid organizationId);
}
