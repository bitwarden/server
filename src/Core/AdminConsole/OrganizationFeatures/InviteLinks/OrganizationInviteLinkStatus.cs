namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public record OrganizationInviteLinkStatus(
    Guid OrganizationId,
    string OrganizationName,
    bool SeatsAvailable,
    OrganizationInviteLinkSsoStatus? Sso);

public record OrganizationInviteLinkSsoStatus(string OrgSsoId, bool Required);
