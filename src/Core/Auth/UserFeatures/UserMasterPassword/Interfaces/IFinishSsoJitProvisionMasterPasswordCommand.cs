using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;
using Bit.Core.Exceptions;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;

/// <summary>
/// <para>Finalizes onboarding for an organization user by setting their initial master password and account keys,
/// then optionally accepting their pending organization membership.</para>
/// <para>Applies to organizations configured with Single Sign-On (SSO) and master password decryption,
/// where just-in-time (JIT) provisioned users are required to establish a master password upon first SSO login.</para>
/// </summary>
public interface IFinishSsoJitProvisionMasterPasswordCommand
{
    /// <summary>
    /// Sets the initial master password and account keys for the specified user and, when
    /// <paramref name="acceptOrganizationInvite"/> is <c>true</c>, accepts their pending organization membership.
    /// </summary>
    /// <param name="user">User to finalize onboarding for. Must not already have a master password set.</param>
    /// <param name="masterPasswordDataModel">Master password, account keys, and org SSO identifier</param>
    /// <param name="acceptOrganizationInvite">Whether to accept the pending organization invite. Open-invite flows
    /// pass <c>false</c> so the member stays Invited; direct-invite flows keep the historical default of <c>true</c>.</param>
    /// <returns>A task that completes when the operation succeeds</returns>
    /// <exception cref="BadRequestException">
    /// Thrown if the user's master password is already set, account keys are missing, the organization
    /// SSO identifier is invalid, or the user is not a member of the organization.
    /// </exception>
    public Task FinishProvisionAsync(
        User user,
        SetInitialMasterPasswordDataModel masterPasswordDataModel,
        bool acceptOrganizationInvite = true);
}
