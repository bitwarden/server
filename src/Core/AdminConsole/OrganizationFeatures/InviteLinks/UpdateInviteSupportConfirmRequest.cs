namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public record UpdateInviteSupportConfirmRequest
{
    public required Guid OrganizationId { get; init; }

    /// <summary>
    /// The invite link cryptographic blob.
    /// </summary>
    public required string Invite { get; init; }

    /// <summary>
    /// Whether this invite link can be used to confirm a user.
    /// </summary>
    public required bool SupportsConfirmation { get; init; }
}
