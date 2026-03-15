using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery;

/// <summary>
/// A command used to recover an organization user's account by an organization admin.
/// </summary>
public interface IAdminRecoverAccountCommand
{
    /// <summary>
    /// Recovers an organization user's account by resetting their master password.
    /// </summary>
    /// <param name="orgId">The organization the user belongs to.</param>
    /// <param name="organizationUser">The organization user being recovered.</param>
    /// <param name="newMasterPassword">The user's new master password hash.</param>
    /// <param name="key">The user's new master-password-sealed user key.</param>
    /// <returns>An IdentityResult indicating success or failure.</returns>
    /// <exception cref="BadRequestException">When organization settings, policy, or user state is invalid.</exception>
    /// <exception cref="NotFoundException">When the user does not exist.</exception>
    Task<IdentityResult> RecoverAccountAsync(Guid orgId, OrganizationUser organizationUser,
        string newMasterPassword, string key);
}
