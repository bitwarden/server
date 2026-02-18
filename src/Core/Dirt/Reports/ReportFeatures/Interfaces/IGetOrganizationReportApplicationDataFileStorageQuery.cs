using Bit.Core.Dirt.Models.Data;

namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IGetOrganizationReportApplicationDataFileStorageQuery
{
    Task<OrganizationReportApplicationDataFileStorageResponse> GetOrganizationReportApplicationDataAsync(Guid organizationId, Guid reportId, string reportFileId);
}
