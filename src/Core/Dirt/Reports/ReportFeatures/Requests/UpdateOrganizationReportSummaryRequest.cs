// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Dirt.Reports.ReportFeatures.Requests;

public class UpdateOrganizationReportSummaryRequest
{
    public Guid OrganizationId { get; set; }
    public Guid ReportId { get; set; }
    public string SummaryData { get; set; }
}
