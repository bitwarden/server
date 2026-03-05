namespace Bit.Core.Dirt.Reports.ReportFeatures.Requests;

public class UpdateOrganizationReportV2Request
{
    public Guid ReportId { get; set; }
    public Guid OrganizationId { get; set; }
    public string? ReportData { get; set; }
    public string? ContentEncryptionKey { get; set; }
    public string? SummaryData { get; set; }
    public string? ApplicationData { get; set; }
    public OrganizationReportMetrics? ReportMetrics { get; set; }
    public bool RequiresNewFileUpload { get; set; }
}
