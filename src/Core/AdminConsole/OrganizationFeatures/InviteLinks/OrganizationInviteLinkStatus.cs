namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public record OrganizationInviteLinkStatus(
    string OrganizationName,
    bool LinksEnabled,
    bool SeatsAvailable,
    OrganizationInviteLinkSsoStatus? Sso);

public record OrganizationInviteLinkSsoStatus(string OrgSsoId, bool Required);
