namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public record UpdateOrganizationInviteLinkRequest
{
    public required Guid OrganizationId { get; init; }
    public required IEnumerable<string> AllowedDomains { get; init; }
}
