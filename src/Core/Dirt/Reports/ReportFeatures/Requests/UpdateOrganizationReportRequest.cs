// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Core.Dirt.Reports.ReportFeatures.Requests;

public class UpdateOrganizationReportRequest
{
    public Guid ReportId { get; set; }
    public Guid OrganizationId { get; set; }
    public string ReportData { get; set; }
    public string ContentEncryptionKey { get; set; }
    public string SummaryData { get; set; } = null;
    public string ApplicationData { get; set; }
}
