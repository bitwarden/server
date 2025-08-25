using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;

namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IUpdateOrganizationReportCommand
{
    Task<OrganizationReport> UpdateOrganizationReportAsync(UpdateOrganizationReportRequest request);
}
