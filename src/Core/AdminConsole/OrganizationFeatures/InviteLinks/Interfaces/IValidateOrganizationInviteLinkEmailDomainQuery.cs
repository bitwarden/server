using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

public interface IValidateOrganizationInviteLinkEmailDomainQuery
{
    /// <summary>
    /// Returns whether the email's domain is allowed by the invite link,
    /// or an error if the invite link does not exist or the code does not match.
    /// </summary>
    Task<CommandResult<bool>> ValidateAsync(Guid organizationId, Guid code, string email);
}
