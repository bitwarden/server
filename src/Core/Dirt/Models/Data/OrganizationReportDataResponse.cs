namespace Bit.Core.Dirt.Models.Data;

public class OrganizationReportDataResponse
{
    public required Guid OrganizationId { get; set; }
    public string? ReportData { get; set; }
    public string? ContentEncryptionKey { get; set; }
    public DateTime? RevisionDate { get; set; }
}
