namespace Bit.Core.Dirt.Models.Data;

public class OrganizationReportApplicationDataResponse
{
    public required Guid OrganizationId { get; set; }
    public string? ApplicationData { get; set; }
    public string? ContentEncryptionKey { get; set; }
    public DateTime? RevisionDate { get; set; }
}
