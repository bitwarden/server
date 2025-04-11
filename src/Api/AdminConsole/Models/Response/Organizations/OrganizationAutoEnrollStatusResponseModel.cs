using Bit.Core.Models.Api;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class OrganizationAutoEnrollStatusResponseModel : ResponseModel
{
    public OrganizationAutoEnrollStatusResponseModel(Guid orgId, bool resetPasswordEnabled, string organizationName) : base("organizationAutoEnrollStatus")
    {
        Id = orgId;
        ResetPasswordEnabled = resetPasswordEnabled;
        Name = organizationName;
    }

    public Guid Id { get; set; }
    public bool ResetPasswordEnabled { get; set; }
    public string Name { get; set; }
}
