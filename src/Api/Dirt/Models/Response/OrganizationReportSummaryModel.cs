namespace Bit.Api.Dirt.Models.Response;

public class OrganizationReportSummaryModel
{
    public Guid OrganizationId { get; set; }
    public required string EncryptedData { get; set; }
    public required string EncryptionKey { get; set; }
    public DateTime Date { get; set; }
}
