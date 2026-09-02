using Bit.Core.Dirt.Models.Data;

namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IGetOrganizationReportApplicationDataQuery
{
    Task<OrganizationReportApplicationDataResponse> GetOrganizationReportApplicationDataAsync(Guid organizationId, Guid reportId);
}
