// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Dirt.Reports.ReportFeatures.Requests;

public class UpdateOrganizationReportRequest
{
    public Guid OrganizationId { get; set; }
    public Guid ReportId { get; set; }
    public string ReportData { get; set; }
}
