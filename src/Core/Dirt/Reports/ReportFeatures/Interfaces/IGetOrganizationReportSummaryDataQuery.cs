using Bit.Core.Dirt.Models.Data;

namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IGetOrganizationReportSummaryDataQuery
{
    Task<OrganizationReportSummaryDataResponse> GetOrganizationReportSummaryDataAsync(Guid organizationId, Guid reportId);
}
