using Bit.Core.Dirt.Reports.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Dirt.Reports.Repositories;

public interface IRiskInsightReportRepository : IRepository<RiskInsightReport, Guid>
{
    Task<ICollection<RiskInsightReport>> GetByOrganizationIdAsync(Guid organizationId);
}

