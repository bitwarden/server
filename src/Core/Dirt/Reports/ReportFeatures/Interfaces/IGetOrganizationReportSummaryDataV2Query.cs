using Bit.Core.Dirt.Models.Data;

namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IGetOrganizationReportSummaryDataV2Query
{
    Task<OrganizationReportSummaryDataResponse?> GetSummaryDataAsync(
        Guid organizationId, Guid reportId);
}
