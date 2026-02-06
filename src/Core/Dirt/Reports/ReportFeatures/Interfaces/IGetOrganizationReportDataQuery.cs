using Bit.Core.Dirt.Models.Data;

namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IGetOrganizationReportDataQuery
{
    Task<OrganizationReportDataResponse> GetOrganizationReportDataAsync(Guid organizationId, Guid reportId);
}
