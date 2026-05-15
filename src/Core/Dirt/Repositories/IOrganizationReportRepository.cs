using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Models.Data;
using Bit.Core.Dirt.Reports.Models.Data;
using Bit.Core.Repositories;

namespace Bit.Core.Dirt.Repositories;

public interface IOrganizationReportRepository : IRepository<OrganizationReport, Guid>
{
    // Whole OrganizationReport methods
    Task<OrganizationReport> GetLatestByOrganizationIdAsync(Guid organizationId, bool filterByValidated = false);

    // SummaryData methods
    Task<IEnumerable<OrganizationReportSummaryDataResponse>> GetSummaryDataByDateRangeAsync(Guid organizationId, DateTime startDate, DateTime endDate);
    Task<OrganizationReportSummaryDataResponse> GetSummaryDataAsync(Guid reportId);
    Task<OrganizationReport> UpdateSummaryDataAsync(Guid orgId, Guid reportId, string summaryData);

    // ApplicationData methods
    Task<OrganizationReportApplicationDataResponse> GetApplicationDataAsync(Guid reportId);
    Task<OrganizationReport> UpdateApplicationDataAsync(Guid orgId, Guid reportId, string applicationData);

    // Metrics methods
    Task UpdateMetricsAsync(Guid reportId, OrganizationReportMetricsData metrics);
}

