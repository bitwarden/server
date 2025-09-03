
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;

namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IAddOrganizationReportCommand
{
    Task<OrganizationReport> AddOrganizationReportAsync(AddOrganizationReportRequest request);
}
