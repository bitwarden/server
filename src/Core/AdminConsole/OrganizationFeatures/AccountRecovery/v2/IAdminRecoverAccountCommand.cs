using Bit.Core.AdminConsole.Utilities.v2.Results;

namespace Bit.Core.AdminConsole.OrganizationFeatures.AccountRecovery.v2;

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
    /// <returns>A <see cref="CommandResult"/> indicating success or containing an error.</returns>
    Task<CommandResult> RecoverAccountAsync(RecoverAccountRequest request);
}
