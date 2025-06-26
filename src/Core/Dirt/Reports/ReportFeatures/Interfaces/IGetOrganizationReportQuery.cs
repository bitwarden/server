using Bit.Core.Dirt.Entities;

namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IGetOrganizationReportQuery
{
    Task<IEnumerable<OrganizationReport>> GetOrganizationReportAsync(Guid organizationId);
    Task<OrganizationReport> GetLatestOrganizationReportAsync(Guid organizationId);
}
