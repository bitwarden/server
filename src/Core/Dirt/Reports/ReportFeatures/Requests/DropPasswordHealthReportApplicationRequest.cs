namespace Bit.Core.Dirt.Reports.ReportFeatures.Requests;

public class DropPasswordHealthReportApplicationRequest
{
    public Guid OrganizationId { get; set; }
    public IEnumerable<Guid> PasswordHealthReportApplicationIds { get; set; }
}
