// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Dirt.Reports.ReportFeatures.Requests;

public class GetOrganizationReportSummaryDataByDateRangeRequest
{
    public Guid OrganizationId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}
