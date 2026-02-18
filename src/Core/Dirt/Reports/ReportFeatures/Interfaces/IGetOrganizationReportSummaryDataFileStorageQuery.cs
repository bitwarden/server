using Bit.Core.Dirt.Models.Data;

namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IGetOrganizationReportSummaryDataFileStorageQuery
{
    Task<OrganizationReportSummaryDataFileStorageResponse> GetOrganizationReportSummaryDataAsync(Guid organizationId, Guid reportId, string reportFileId);
}
