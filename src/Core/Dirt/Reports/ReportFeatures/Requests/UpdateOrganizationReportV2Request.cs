namespace Bit.Core.Dirt.Reports.ReportFeatures.Requests;

public class UpdateOrganizationReportV2Request
{
    public Guid ReportId { get; set; }
    public Guid OrganizationId { get; set; }
    public required string ContentEncryptionKey { get; set; }
    public required string SummaryData { get; set; }
    public required string ApplicationData { get; set; }
    public required OrganizationReportMetrics ReportMetrics { get; set; }
}
