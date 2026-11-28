using Bit.Core.AdminConsole.Entities;

namespace Bit.Seeder.Factories;

internal static class OrganizationInviteLinkSeeder
{
    /// <summary>
    /// Builds an invite link row the way <c>CreateOrganizationInviteLinkCommand</c> does: a new Id, a new random
    /// Code, and the allowed domains lowercased and trimmed with blank entries removed.
    /// </summary>
    /// <param name="organizationId">The organization the link belongs to.</param>
    /// <param name="allowedDomains">Domains already cleaned with <see cref="SanitizeDomains"/>.</param>
    /// <param name="invite">The sealed invite blob from the Rust SDK.</param>
    /// <param name="supportsConfirmation">Whether the sealed invite lets an invitee recover the organization key.</param>
    internal static OrganizationInviteLink Create(
        Guid organizationId,
        IEnumerable<string> allowedDomains,
        string invite,
        bool supportsConfirmation)
    {
        var now = DateTime.UtcNow;
        var inviteLink = new OrganizationInviteLink
        {
            OrganizationId = organizationId,
            Invite = invite,
            SupportsConfirmation = supportsConfirmation,
            CreationDate = now,
            RevisionDate = now,
        };
        inviteLink.SetAllowedDomains(allowedDomains);
        inviteLink.SetNewId();
        inviteLink.SetNewCode();

        return inviteLink;
    }

    /// <summary>
    /// Same rules as Core's internal <c>InviteLinkDomainSanitizer</c>: lowercase, trim, drop blank entries.
    /// </summary>
    internal static List<string> SanitizeDomains(IEnumerable<string>? domains) =>
        domains?
            .Select(d => d?.Trim().ToLowerInvariant())
            .Where(d => !string.IsNullOrEmpty(d))
            .Cast<string>()
            .ToList() ?? [];
}
