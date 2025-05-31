using Bit.Core.Dirt.Reports.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Dirt.Reports.Repositories;

public interface IRiskInsightCriticalApplicationRepository : IRepository<RiskInsightCriticalApplication, Guid>
{
    Task<ICollection<RiskInsightCriticalApplication>> GetByOrganizationIdAsync(Guid organizationId);
}
