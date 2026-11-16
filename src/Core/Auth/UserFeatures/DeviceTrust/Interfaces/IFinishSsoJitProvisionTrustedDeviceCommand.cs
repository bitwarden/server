using Bit.Core.Auth.UserFeatures.DeviceTrust.Data;
using Bit.Core.Entities;
using Bit.Core.Exceptions;

namespace Bit.Core.Auth.UserFeatures.DeviceTrust;

/// <summary>
/// <para>Finalizes onboarding for a just-in-time (JIT) provisioned SSO user in an organization that uses
/// trusted device encryption.</para>
/// <para>Stores the account keys, optionally trusts the current device, and, for direct invites, enrolls the
/// user in admin account recovery and accepts the pending organization invite.</para>
/// <para>For now, account recovery enrollment is tied to accepting the invite.</para>
/// </summary>
public interface IFinishSsoJitProvisionTrustedDeviceCommand
{
    /// <summary>
    /// Finishes trusted device encryption onboarding for the specified user.
    /// </summary>
    /// <param name="user">User to finalize onboarding for. Must not already have a keypair.</param>
    /// <param name="data">Account keys, organization, account recovery key and optional device keys.</param>
    /// <param name="acceptOrganizationInvite">When false (open invite), the member stays Invited and is not
    /// enrolled in account recovery, so Admin Console checks can run later. Device trust runs either way.
    /// For now, account recovery enrollment is tied to this flag.</param>
    /// <exception cref="BadRequestException">
    /// Thrown if the user already has a keypair, the organization does not use trusted device encryption,
    /// the user is not a member of the organization, or the invite is accepted without a valid account
    /// recovery key.
    /// </exception>
    /// <exception cref="NotFoundException">Thrown if device keys are given for a device the user does not own.</exception>
    Task FinishProvisionAsync(User user, FinishSsoJitProvisionTrustedDeviceData data, bool acceptOrganizationInvite = true);
}
