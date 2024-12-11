using Bit.Core.Models.Api;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class OrganizationAutoEnrollStatusResponseModel : ResponseModel
{
    public OrganizationAutoEnrollStatusResponseModel(Guid orgId, bool resetPasswordEnabled)
        : base("organizationAutoEnrollStatus")
    {
        Id = orgId;
        ResetPasswordEnabled = resetPasswordEnabled;
    }

    public Guid Id { get; set; }
    public bool ResetPasswordEnabled { get; set; }
}
