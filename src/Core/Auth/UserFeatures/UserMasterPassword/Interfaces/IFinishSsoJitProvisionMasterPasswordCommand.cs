using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;
using Bit.Core.Exceptions;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;

/// <summary>
/// <para>Finalizes onboarding for an organization user by setting their initial master password and account keys,
/// then accepting their organization membership.</para>
/// <para>Applies to organizations configured with Single Sign-On (SSO) and master password decryption,
/// where just-in-time (JIT) provisioned users are required to establish a master password upon first SSO login.</para>
/// </summary>
public interface IFinishSsoJitProvisionMasterPasswordCommand
{
    /// <summary>
    /// Sets the initial master password and account keys for the specified user and accepts their pending
    /// organization membership.
    /// </summary>
    /// <param name="user">User to finalize onboarding for. Must not already have a master password set.</param>
    /// <param name="masterPasswordDataModel">Master password, account keys, and org SSO identifier</param>
    /// <returns>A task that completes when the operation succeeds</returns>
    /// <exception cref="BadRequestException">
    /// Thrown if the user's master password is already set, account keys are missing, the organization
    /// SSO identifier is invalid, or the user is not a member of the organization.
    /// </exception>
    public Task FinishSsoJitProvisionMasterPasswordAsync(User user, SetInitialMasterPasswordDataModel masterPasswordDataModel);
}
