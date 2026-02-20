using Bit.Core.Dirt.Models.Data;

namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IGetOrganizationReportSummaryDataByDateRangeV2Query
{
    Task<IEnumerable<OrganizationReportSummaryDataResponse>> GetSummaryDataByDateRangeAsync(
        Guid organizationId, DateTime startDate, DateTime endDate);
}
