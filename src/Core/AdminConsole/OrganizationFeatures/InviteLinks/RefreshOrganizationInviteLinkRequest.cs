namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public record RefreshOrganizationInviteLinkRequest
{
    public required Guid OrganizationId { get; init; }

    /// <summary>
    /// The invite link cryptographic blob.
    /// </summary>
    public required string Invite { get; init; }
}
