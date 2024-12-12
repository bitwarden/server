using Bit.Core.Billing.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Billing.Repositories;

public interface IOrganizationInstallationRepository : IRepository<OrganizationInstallation, Guid>
{
    Task<OrganizationInstallation> GetByInstallationIdAsync(Guid installationId);
    Task<ICollection<OrganizationInstallation>> GetByOrganizationIdAsync(Guid organizationId);
}
