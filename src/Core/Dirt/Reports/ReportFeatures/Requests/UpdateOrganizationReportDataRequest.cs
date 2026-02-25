namespace Bit.Core.Dirt.Reports.ReportFeatures.Requests;

public class UpdateOrganizationReportDataRequest
{
    public Guid OrganizationId { get; set; }
    public Guid ReportId { get; set; }
    public string? ReportData { get; set; }
}
