using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

public interface IValidateOrganizationInviteLinkQuery
{
    /// <summary>
    /// Validates that an open organization invite link is valid and that the email
    /// matches its allowed domains. It does NOT check that the email has been
    /// verified - the caller must check this separately if required.
    /// </summary>
    /// <param name="organizationId">The organization's ID (from the URL path).</param>
    /// <param name="code">The public invite link code.</param>
    /// <param name="email">The registering user's email; checked against the link's AllowedDomains.</param>
    /// <returns>
    /// A successful CommandResult if the link is valid and matches the email; a failed
    /// CommandResult if validation fails or the invite link is otherwise not available.
    /// </returns>
    Task<CommandResult> ValidateAsync(Guid organizationId, Guid code, string email);
}
