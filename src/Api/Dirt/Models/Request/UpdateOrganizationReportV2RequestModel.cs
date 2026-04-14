using Bit.Core.Dirt.Reports.ReportFeatures.Requests;

namespace Bit.Api.Dirt.Models.Request;

public class UpdateOrganizationReportV2RequestModel
{
    public required string ContentEncryptionKey { get; set; }
    public required string SummaryData { get; set; }
    public required string ApplicationData { get; set; }
    public required OrganizationReportMetrics ReportMetrics { get; set; }

    public UpdateOrganizationReportV2Request ToData(Guid organizationId, Guid reportId)
    {
        return new UpdateOrganizationReportV2Request
        {
            OrganizationId = organizationId,
            ReportId = reportId,
            ContentEncryptionKey = ContentEncryptionKey,
            SummaryData = SummaryData,
            ApplicationData = ApplicationData,
            ReportMetrics = ReportMetrics
        };
    }
}
