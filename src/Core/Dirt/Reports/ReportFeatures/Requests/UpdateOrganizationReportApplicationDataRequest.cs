namespace Bit.Core.Dirt.Reports.ReportFeatures.Requests;

public class UpdateOrganizationReportApplicationDataRequest
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string? ApplicationData { get; set; }
    public OrganizationReportMetricsRequest? Metrics { get; set; }
}
