using Bit.Core.Models.Api;

namespace Bit.Api.Models.Response.Organizations;

public class OrganizationAutoEnrollStatusResponseModel : ResponseModel
{
    public OrganizationAutoEnrollStatusResponseModel(Guid orgId, bool resetPasswordEnabled) : base("organizationAutoEnrollStatus")
    {
        Id = orgId.ToString();
        ResetPasswordEnabled = resetPasswordEnabled;
    }

    public string Id { get; set; }
    public bool ResetPasswordEnabled { get; set; }
}
