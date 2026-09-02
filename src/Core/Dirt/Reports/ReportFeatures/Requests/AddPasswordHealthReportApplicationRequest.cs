// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Dirt.Reports.ReportFeatures.Requests;

public class AddPasswordHealthReportApplicationRequest
{
    public Guid OrganizationId { get; set; }
    public string Url { get; set; }
}
