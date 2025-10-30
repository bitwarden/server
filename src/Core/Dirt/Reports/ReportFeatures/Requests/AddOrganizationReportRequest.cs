namespace Bit.Core.Dirt.Reports.ReportFeatures.Requests;

public class AddOrganizationReportRequest
{
    public Guid OrganizationId { get; set; }
    public string? ReportData { get; set; }

    public string? ContentEncryptionKey { get; set; }

    public string? SummaryData { get; set; }

    public string? ApplicationData { get; set; }

    public OrganizationReportMetricsRequest Metrics { get; set; } = new OrganizationReportMetricsRequest();
}
