using Bit.Core.Dirt.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Dirt.Repositories;

public interface IOrganizationReportSummaryRepository : IRepository<OrganizationReportSummary, Guid>
{
    Task<ICollection<OrganizationReportSummary>> GetByOrganizationReportIdAsync(Guid organizationId);
}
