using Bit.Core.Entities;
using Bit.Core.Services;

namespace Bit.Core.OrganizationFeatures.OrganizationUsers.Interfaces;

public interface IAcceptOrgUserCommand
{
    /// <summary>
    /// Moves an OrganizationUser into the Accepted status and marks their email as verified.
    /// This method is used where the user has clicked the invitation link sent by email.
    /// </summary>
    /// <param name="token">The token embedded in the email invitation link</param>
    /// <returns>The accepted OrganizationUser.</returns>
    Task<OrganizationUser> AcceptOrgUserByTokenAsync(Guid organizationUserId, User user, string token, IUserService userService);
    Task<OrganizationUser> AcceptOrgUserByOrgSsoIdAsync(string orgIdentifier, User user, IUserService userService);
    Task<OrganizationUser> AcceptOrgUserByOrgIdAsync(Guid organizationId, User user, IUserService userService);
    Task<OrganizationUser> AcceptOrgUserAsync(OrganizationUser orgUser, User user, IUserService userService);
}
