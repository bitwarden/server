using Bit.Core.Dirt.Reports.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Dirt.Reports.Repositories;

public interface IOrganizationReportRepository : IRepository<OrganizationReport, Guid>
{
    Task<ICollection<OrganizationReport>> GetByOrganizationIdAsync(Guid organizationId);
}

