using Bit.Core.Dirt.Models.Data;

namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IGetOrganizationReportDataV2Query
{
    Task<OrganizationReportDataFileStorageResponse> GetOrganizationReportDataAsync(Guid organizationId, Guid reportId, string reportFileId);
}
