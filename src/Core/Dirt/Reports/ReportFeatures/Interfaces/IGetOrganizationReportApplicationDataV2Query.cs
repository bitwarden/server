using Bit.Core.Dirt.Models.Data;

namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IGetOrganizationReportApplicationDataV2Query
{
    Task<OrganizationReportApplicationDataResponse?> GetApplicationDataAsync(
        Guid organizationId, Guid reportId);
}
