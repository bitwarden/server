using Bit.Core.Dirt.Reports.ReportFeatures.Requests;

namespace Bit.Api.Dirt.Models.Request;

public class UpdateOrganizationReportV2RequestModel
{
    public string? ReportData { get; set; }
    public string? ContentEncryptionKey { get; set; }
    public string? SummaryData { get; set; }
    public string? ApplicationData { get; set; }
    public OrganizationReportMetrics? ReportMetrics { get; set; }

    public UpdateOrganizationReportV2Request ToData(Guid organizationId, Guid reportId)
    {
        return new UpdateOrganizationReportV2Request
        {
            OrganizationId = organizationId,
            ReportId = reportId,
            ReportData = ReportData,
            ContentEncryptionKey = ContentEncryptionKey,
            SummaryData = SummaryData,
            ApplicationData = ApplicationData,
            ReportMetrics = ReportMetrics
        };
    }
}
