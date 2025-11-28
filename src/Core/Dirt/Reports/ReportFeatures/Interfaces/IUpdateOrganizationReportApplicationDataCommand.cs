using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;

namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IUpdateOrganizationReportApplicationDataCommand
{
    Task<OrganizationReport> UpdateOrganizationReportApplicationDataAsync(UpdateOrganizationReportApplicationDataRequest request);
}
