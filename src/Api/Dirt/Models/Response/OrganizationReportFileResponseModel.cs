using Bit.Core.Enums;

namespace Bit.Api.Dirt.Models.Response;

public class OrganizationReportFileResponseModel
{
    public OrganizationReportFileResponseModel() { }

    public string ReportFileUploadUrl { get; set; } = string.Empty;
    public OrganizationReportResponseModel ReportResponse { get; set; } = null!;
    public FileUploadType FileUploadType { get; set; }
}
