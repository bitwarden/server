using Bit.Core.Dirt.Models.Data;

namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IGetOrganizationReportSummaryDataByDateRangeQuery
{
    Task<IEnumerable<OrganizationReportSummaryDataResponse>> GetOrganizationReportSummaryDataByDateRangeAsync(Guid organizationId, Guid reportId, DateTime startDate, DateTime endDate);
}
