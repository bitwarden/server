using System.Text.Json.Serialization;

namespace Bit.Core.Dirt.Models.Data;

public class OrganizationReportSummaryDataResponse
{
    public required Guid OrganizationId { get; set; }
    [JsonPropertyName("encryptedData")]
    public required string SummaryData { get; set; }
    [JsonPropertyName("contentEncryptionKey")]
    public required string ContentEncryptionKey { get; set; }
    [JsonPropertyName("date")]
    public required DateTime RevisionDate { get; set; }
}
