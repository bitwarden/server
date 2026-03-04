using Bit.Core.Exceptions;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery;

/// <summary>
/// A command used to recover an organization user's account by an organization admin.
/// Supports resetting the user's master password and/or clearing their two-factor authentication methods.
/// </summary>
public interface IAdminRecoverAccountCommand
{
    /// <summary>
    /// Recovers an organization user's account by resetting their master password and/or
    /// clearing their two-factor authentication methods.
    /// </summary>
    /// <param name="request">The recovery request containing the organization, user, and action flags.</param>
    /// <returns>An IdentityResult indicating success or failure of the password update (if applicable).</returns>
    /// <exception cref="BadRequestException">When organization settings, policy, or user state is invalid.</exception>
    /// <exception cref="NotFoundException">When the user does not exist.</exception>
    Task<IdentityResult> RecoverAccountAsync(RecoverAccountRequest request);
}
