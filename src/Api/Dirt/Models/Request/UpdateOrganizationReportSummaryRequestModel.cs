using Bit.Core.Dirt.Reports.ReportFeatures.Requests;

namespace Bit.Api.Dirt.Models.Request;

public class UpdateOrganizationReportSummaryRequestModel
{
    public string? SummaryData { get; set; }
    public OrganizationReportMetrics? ReportMetrics { get; set; }

    public UpdateOrganizationReportSummaryRequest ToData(Guid organizationId, Guid reportId)
    {
        return new UpdateOrganizationReportSummaryRequest
        {
            OrganizationId = organizationId,
            ReportId = reportId,
            SummaryData = SummaryData,
            ReportMetrics = ReportMetrics
        };
    }
}
