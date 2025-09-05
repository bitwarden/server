namespace Bit.Core.Dirt.Models.Data;

public class OrganizationReportDataResponse
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string? ReportData { get; set; }
}
