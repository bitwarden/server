using System.Text.Json.Serialization;
using Bit.Core.Dirt.Models.Data;

namespace Bit.Api.Dirt.Models.Response;

public class OrganizationReportSummaryDataResponseModel
{
    public OrganizationReportSummaryDataResponseModel(OrganizationReportSummaryDataResponse summaryDataResponse)
    {
        EncryptedData = summaryDataResponse.SummaryData;
        EncryptionKey = summaryDataResponse.ContentEncryptionKey;
        Date = summaryDataResponse.RevisionDate;
    }

    [JsonPropertyName("encryptedData")]
    public string EncryptedData { get; set; }

    [JsonPropertyName("encryptionKey")]
    public string EncryptionKey { get; set; }

    [JsonPropertyName("date")]
    public DateTime Date { get; set; }
}
