namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public record OrganizationInviteLinkStatus(
    string OrganizationName,
    bool SeatsAvailable,
    bool SupportsConfirmation,
    OrganizationInviteLinkSsoStatus? Sso);

public record OrganizationInviteLinkSsoStatus(string OrgSsoId, bool Required);
