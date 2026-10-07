using Bit.Core.Dirt.Models.Data;

namespace Bit.Api.Dirt.Models.Response;

public class OrganizationReportApplicationDataResponseModel
{
    public OrganizationReportApplicationDataResponseModel(OrganizationReportApplicationDataResponse applicationDataResponse)
    {
        ApplicationData = applicationDataResponse.ApplicationData;
    }

    public string? ApplicationData { get; set; }
}
