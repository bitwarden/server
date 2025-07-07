namespace Bit.Core.Dirt.Reports.ReportFeatures.Requests;

public class AddOrganizationReportRequest
{
    public Guid OrganizationId { get; set; }
    public string ReportData { get; set; }
    public DateTime Date { get; set; }
}
