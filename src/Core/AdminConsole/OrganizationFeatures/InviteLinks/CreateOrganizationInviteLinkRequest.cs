namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public record CreateOrganizationInviteLinkRequest
{
    public required Guid OrganizationId { get; init; }
    public required IEnumerable<string> AllowedDomains { get; init; }

    /// <summary>
    /// The invite link cryptographic blob.
    /// </summary>
    public required string Invite { get; init; }

    /// <summary>
    /// Indicates if the link supports user auto confirmation (not supported yet).
    /// </summary>
    public required bool SupportsConfirmation { get; init; }
}
