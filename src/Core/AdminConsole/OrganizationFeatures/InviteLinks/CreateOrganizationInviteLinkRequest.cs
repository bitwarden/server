namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public record CreateOrganizationInviteLinkRequest
{
    public required Guid OrganizationId { get; init; }
    public required IEnumerable<string> AllowedDomains { get; init; }
    public required string EncryptedInviteKey { get; init; }

    /// <summary>
    /// The organization key encrypted for the invite link. Currently unused; will be populated in a future stage.
    /// </summary>
    public string? EncryptedOrgKey { get; init; }
}
