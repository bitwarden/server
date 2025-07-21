using Bit.Core.Billing.Organizations.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Billing.Organizations.Repositories;

public interface IOrganizationInstallationRepository : IRepository<OrganizationInstallation, Guid>
{
    Task<OrganizationInstallation> GetByInstallationIdAsync(Guid installationId);
    Task<ICollection<OrganizationInstallation>> GetByOrganizationIdAsync(Guid organizationId);
}
