using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;

namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IUpdateOrganizationReportSummaryCommand
{
    Task<OrganizationReport> UpdateOrganizationReportSummaryAsync(UpdateOrganizationReportSummaryRequest request);
}
