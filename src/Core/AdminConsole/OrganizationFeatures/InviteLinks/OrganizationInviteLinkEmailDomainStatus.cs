namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public record OrganizationInviteLinkEmailDomainStatus(
    Guid OrganizationId,
    bool IsAllowed);
