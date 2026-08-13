using Bit.Core.Dirt.Reports.ReportFeatures.Requests;

namespace Bit.Api.Dirt.Models.Request;

public class UpdateOrganizationReportApplicationDataRequestModel
{
    public string? ApplicationData { get; set; }

    public UpdateOrganizationReportApplicationDataRequest ToData(Guid organizationId, Guid reportId)
    {
        return new UpdateOrganizationReportApplicationDataRequest
        {
            OrganizationId = organizationId,
            Id = reportId,
            ApplicationData = ApplicationData
        };
    }
}
