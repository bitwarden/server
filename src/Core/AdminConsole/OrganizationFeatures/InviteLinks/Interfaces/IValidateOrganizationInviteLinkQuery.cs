using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

public interface IValidateOrganizationInviteLinkQuery
{
    /// <summary>
    /// Validates that an open organization invite link is usable — narrower than
    /// <see cref="IGetOrganizationInviteLinkStatusQuery"/>, which additionally computes
    /// seat availability and SSO status for display to a landing user. This validator
    /// exists for flows (e.g., registration) that only need to confirm the link is real and valid.
    /// </summary>
    /// <param name="organizationId">The organization's ID (from the URL path).</param>
    /// <param name="code">The public invite link code.</param>
    /// <returns>
    /// Void success if the link is valid; <see cref="InviteLinkNotFound"/> if the link
    /// does not exist, the code does not match, or the organization is missing or disabled;
    /// <see cref="InviteLinkNotAvailable"/> if the organization has the invite links feature
    /// disabled.
    /// </returns>
    Task<CommandResult> ValidateAsync(Guid organizationId, Guid code);
}
