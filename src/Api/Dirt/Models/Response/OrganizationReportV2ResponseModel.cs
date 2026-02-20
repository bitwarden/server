using Bit.Core.Models.Api;

namespace Bit.Api.Dirt.Models.Response;

public class OrganizationReportV2ResponseModel : ResponseModel
{
    public OrganizationReportV2ResponseModel() : base("organizationReport-v2") { }

    public string ReportDataUploadUrl { get; set; } = string.Empty;
    public OrganizationReportResponseModel ReportResponse { get; set; } = null!;
}
