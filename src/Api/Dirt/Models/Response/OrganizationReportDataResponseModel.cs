using Bit.Core.Dirt.Models.Data;

namespace Bit.Api.Dirt.Models.Response;

public class OrganizationReportDataResponseModel
{
    public OrganizationReportDataResponseModel(OrganizationReportDataResponse reportDataResponse)
    {
        ReportData = reportDataResponse.ReportData;
    }

    public string? ReportData { get; set; }
}
