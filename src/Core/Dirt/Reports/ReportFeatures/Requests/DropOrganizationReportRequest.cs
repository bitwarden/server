namespace Bit.Core.Dirt.Reports.ReportFeatures.Requests;

public class DropOrganizationReportRequest
{
    public Guid OrganizationId { get; set; }
    public IEnumerable<Guid> OrganizationReportIds { get; set; }
}
