#nullable enable
using Bit.Core.ActionableInsights.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.ActionableInsights.Repositories;

public interface IReportRepository : IRepository<Report, Guid>
{
    Task<Report?> GetByOrganizationId(Guid organizationId);
}
