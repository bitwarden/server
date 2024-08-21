#nullable enable
using Bit.Core.ActionableInsights.Entities;

namespace Bit.Core.ActionableInsights.Repositories;

public interface IReportRepository
{
    Task<Report?> GetByOrganizationId(Guid organizationId);

    Task<Report> CreateAsync(Report report);

    Task ReplaceAsync(Report report);
}
