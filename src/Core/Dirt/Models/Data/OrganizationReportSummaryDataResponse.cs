namespace Bit.Core.Dirt.Models.Data;

public class OrganizationReportSummaryDataResponse
{
    public required Guid OrganizationId { get; set; }
    public required string SummaryData { get; set; }
    public required string ContentEncryptionKey { get; set; }
    public required DateTime RevisionDate { get; set; }
}
