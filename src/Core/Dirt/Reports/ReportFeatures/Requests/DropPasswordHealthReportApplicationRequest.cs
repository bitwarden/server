// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Dirt.Reports.ReportFeatures.Requests;

public class DropPasswordHealthReportApplicationRequest
{
    public Guid OrganizationId { get; set; }
    public IEnumerable<Guid> PasswordHealthReportApplicationIds { get; set; }
}
