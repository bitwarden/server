using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;

namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IUpdateOrganizationReportApplicationDataV2Command
{
    Task<OrganizationReport> UpdateApplicationDataAsync(
        UpdateOrganizationReportApplicationDataRequest request);
}
