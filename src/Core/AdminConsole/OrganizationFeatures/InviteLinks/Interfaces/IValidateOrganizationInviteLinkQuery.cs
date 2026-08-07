using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

public interface IValidateOrganizationInviteLinkQuery
{
    /// <summary>
    /// Validates that an open organization invite link is usable for the given email — narrower
    /// than <see cref="IGetOrganizationInviteLinkStatusQuery"/>, which additionally computes seat
    /// availability and SSO status for display to a landing user. This validator exists for flows
    /// (e.g., registration) that only need to confirm the link is real, valid, and admits the
    /// caller's email — the last check gates the domain-block bypass that the caller applies on
    /// success, so possession of the {orgId, code} alone must not be sufficient when the link's
    /// AllowedDomains would reject the email at accept time.
    /// </summary>
    /// <param name="organizationId">The organization's ID (from the URL path).</param>
    /// <param name="code">The public invite link code.</param>
    /// <param name="email">The registering user's email; checked against the link's AllowedDomains.</param>
    /// <returns>
    /// Void success if the link is valid and admits the email; <see cref="InviteLinkNotFound"/>
    /// if the link does not exist, the code does not match, or the organization is missing or
    /// disabled; <see cref="InviteLinkNotAvailable"/> if the organization has the invite links
    /// feature disabled; <see cref="EmailDomainNotAllowed"/> if the email's domain is not in
    /// the link's AllowedDomains.
    /// </returns>
    Task<CommandResult> ValidateAsync(Guid organizationId, Guid code, string email);
}
