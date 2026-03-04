using Bit.Core.Enums;

namespace Bit.Api.Dirt.Models.Response;

public class OrganizationReportV2ResponseModel
{
    public OrganizationReportV2ResponseModel() { }

    public string ReportDataUploadUrl { get; set; } = string.Empty;
    public OrganizationReportResponseModel ReportResponse { get; set; } = null!;
    public FileUploadType FileUploadType { get; set; }
}
