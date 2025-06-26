
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;

namespace Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;

public interface IDropOrganizationReportCommand
{
    Task DropOrganizationReportAsync(DropOrganizationReportRequest request);
}
