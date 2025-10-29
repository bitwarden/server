namespace Bit.Core.Dirt.Reports.ReportFeatures.Requests;

public class AddOrganizationReportRequest
{
    public Guid OrganizationId { get; set; }
    public required string ReportData { get; set; }

    public required string ContentEncryptionKey { get; set; }

    public required string SummaryData { get; set; }

    public required string ApplicationData { get; set; }

    public OrganizationReportMetricsRequest Metrics { get; set; } = new OrganizationReportMetricsRequest();
}
