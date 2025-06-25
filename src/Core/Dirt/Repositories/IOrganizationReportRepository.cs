using Bit.Core.Dirt.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Dirt.Repositories;

public interface IOrganizationReportRepository : IRepository<OrganizationReport, Guid>
{
    Task<ICollection<OrganizationReport>> GetByOrganizationIdAsync(Guid organizationId);

    Task<OrganizationReport> GetLatestByOrganizationIdAsync(Guid organizationId);
}

