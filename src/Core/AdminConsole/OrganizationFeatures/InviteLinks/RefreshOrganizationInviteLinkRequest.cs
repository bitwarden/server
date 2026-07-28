namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public record RefreshOrganizationInviteLinkRequest
{
    public required Guid OrganizationId { get; init; }

    /// <summary>
    /// The cryptographic invite link.
    /// </summary>
    public required string Invite { get; init; }

    /// <summary>
    /// Whether this invite link can be used to confirm a user.
    /// </summary>
    public required bool SupportsConfirmation { get; init; }
}
