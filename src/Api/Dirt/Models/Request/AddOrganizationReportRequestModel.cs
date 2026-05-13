using System.Text.Json.Serialization;
using Bit.Core.Dirt.Reports.ReportFeatures.Requests;

namespace Bit.Api.Dirt.Models.Request;

public class AddOrganizationReportRequestModel
{
    public string? ReportData { get; set; }
    public string? ContentEncryptionKey { get; set; }
    public string? SummaryData { get; set; }
    public string? ApplicationData { get; set; }
    [JsonPropertyName("metrics")]
    public OrganizationReportMetrics? ReportMetrics { get; set; }
    public long? FileSize { get; set; }

    public AddOrganizationReportRequest ToData(Guid organizationId)
    {
        return new AddOrganizationReportRequest
        {
            OrganizationId = organizationId,
            ReportData = ReportData,
            ContentEncryptionKey = ContentEncryptionKey,
            SummaryData = SummaryData,
            ApplicationData = ApplicationData,
            ReportMetrics = ReportMetrics,
            FileSize = FileSize
        };
    }
}
