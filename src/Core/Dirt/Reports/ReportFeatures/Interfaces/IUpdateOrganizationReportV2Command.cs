using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;

namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IUpdateOrganizationReportV2Command
{
    Task<OrganizationReport> UpdateAsync(UpdateOrganizationReportV2Request request);
}
