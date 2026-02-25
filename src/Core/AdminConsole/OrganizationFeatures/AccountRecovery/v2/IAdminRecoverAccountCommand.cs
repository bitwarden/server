using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery.v2;

/// <summary>
/// A command used to recover an organization user's account by an organization admin.
/// Accepts standardized authentication and unlock data models that enable server-side
/// cross-validation of KDF settings and salt against the target user's stored values.
/// </summary>
public interface IAdminRecoverAccountCommand
{
    /// <summary>
    /// Recovers an organization user's account by resetting their master password.
    /// </summary>
    /// <param name="orgId">The organization the user belongs to.</param>
    /// <param name="organizationUser">The organization user being recovered.</param>
    /// <param name="authenticationData">Standardized authentication data including the new master password hash, KDF settings, and salt.</param>
    /// <param name="unlockData">Standardized unlock data including the master-key-wrapped user key, KDF settings, and salt.</param>
    /// <returns>An IdentityResult indicating success or failure.</returns>
    /// <exception cref="BadRequestException">When organization settings, policy, user state, KDF settings, or salt is invalid.</exception>
    /// <exception cref="NotFoundException">When the user does not exist.</exception>
    Task<IdentityResult> RecoverAccountAsync(Guid orgId, OrganizationUser organizationUser,
        MasterPasswordAuthenticationData authenticationData, MasterPasswordUnlockData unlockData);
}
