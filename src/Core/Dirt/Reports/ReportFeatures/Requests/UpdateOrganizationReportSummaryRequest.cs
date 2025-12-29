namespace Bit.Core.Dirt.Reports.ReportFeatures.Requests;

public class UpdateOrganizationReportSummaryRequest
{
    public Guid OrganizationId { get; set; }
    public Guid ReportId { get; set; }
    public string? SummaryData { get; set; }
    public OrganizationReportMetricsRequest? Metrics { get; set; }
}
