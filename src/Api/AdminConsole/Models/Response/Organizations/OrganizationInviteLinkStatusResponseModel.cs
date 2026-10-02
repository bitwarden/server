using Bit.Core.Models.Api;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class OrganizationInviteLinkStatusResponseModel : ResponseModel
{
    public string OrganizationName { get; set; } = null!;
    public bool LinksEnabled { get; set; }
    public bool SeatsAvailable { get; set; }
    public bool SupportsConfirmation { get; set; }
    public OrganizationInviteLinkSsoResponseModel? Sso { get; set; }

    public OrganizationInviteLinkStatusResponseModel(
        string organizationName,
        bool linksEnabled,
        bool seatsAvailable,
        bool supportsConfirmation,
        OrganizationInviteLinkSsoResponseModel? sso) : base("inviteLinkStatus")
    {
        OrganizationName = organizationName;
        LinksEnabled = linksEnabled;
        SeatsAvailable = seatsAvailable;
        SupportsConfirmation = supportsConfirmation;
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
