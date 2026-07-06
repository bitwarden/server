using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;

public interface IValidateOrganizationInviteLinkEmailDomainQuery
{
    /// <summary>
    /// Returns whether the email's domain is allowed by the invite link and the organization id
    /// or an error if the invite link does not exist.
    /// </summary>
    Task<CommandResult<OrganizationInviteLinkEmailDomainStatus>> ValidateAsync(Guid code, string email);
}
