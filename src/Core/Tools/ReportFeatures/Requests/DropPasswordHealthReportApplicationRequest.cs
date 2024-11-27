namespace Bit.Core.Tools.ReportFeatures.Requests;

public class DropPasswordHealthReportApplicationRequest
{
    public Guid OrganizationId { get; set; }
    public IEnumerable<Guid> PasswordHealthReportApplicationIds { get; set; }
}
