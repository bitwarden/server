using Bit.Core.Models.Api;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class OrganizationInviteLinkStatusResponseModel : ResponseModel
{
    public string OrganizationName { get; set; } = null!;
    public bool SeatsAvailable { get; set; }
    public OrganizationInviteLinkSsoResponseModel? Sso { get; set; }

    public OrganizationInviteLinkStatusResponseModel(
        string organizationName,
        bool seatsAvailable,
        OrganizationInviteLinkSsoResponseModel? sso) : base("inviteLinkStatus")
    {
        OrganizationName = organizationName;
        SeatsAvailable = seatsAvailable;
        Sso = sso;
    }
}

public class OrganizationInviteLinkSsoResponseModel : ResponseModel
{
    public string OrgSsoId { get; set; }
    public bool Required { get; set; }

    public OrganizationInviteLinkSsoResponseModel(string orgSsoId, bool required) : base("inviteLinkSso")
    {
        OrgSsoId = orgSsoId;
        Required = required;
    }
}
