using Bit.Core.Dirt.Reports.ReportFeatures.Requests;

namespace Bit.Api.Dirt.Models.Request;

public class UpdateOrganizationReportDataRequestModel
{
    public string? ReportData { get; set; }

    public UpdateOrganizationReportDataRequest ToData(Guid organizationId, Guid reportId)
    {
        return new UpdateOrganizationReportDataRequest
        {
            OrganizationId = organizationId,
            ReportId = reportId,
            ReportData = ReportData
        };
    }
}
