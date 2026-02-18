using Bit.Core.Enums;
using Bit.Core.Models.Api;

namespace Bit.Api.Dirt.Models.Response;

public class OrganizationReportFileUploadResponseModel : ResponseModel
{
    public OrganizationReportFileUploadResponseModel() : base("organizationReport-fileUpload") { }

    public FileUploadType FileUploadType { get; set; }
    public string ReportDataUploadUrl { get; set; } = string.Empty;
    public string SummaryDataUploadUrl { get; set; } = string.Empty;
    public string ApplicationDataUploadUrl { get; set; } = string.Empty;
    public string ReportFileId { get; set; } = string.Empty;
    public OrganizationReportResponseModel ReportResponse { get; set; } = null!;
}
