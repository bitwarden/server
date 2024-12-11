using Bit.Core.Entities;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;

/// <summary>
/// <para>Manages the setting of the initial master password for a <see cref="User"/> in an organization.</para>
/// <para>This class is primarily invoked in two scenarios:</para>
/// <para>1) In organizations configured with Single Sign-On (SSO) and master password decryption:
/// just in time (JIT) provisioned users logging in via SSO are required to set a master password.</para>
/// <para>2) In organizations configured with SSO and trusted devices decryption:
/// Users who are upgraded to have admin account recovery permissions must set a master password
/// to ensure their ability to reset other users' accounts.</para>
/// </summary>
public interface ISetInitialMasterPasswordCommand
{
    public Task<IdentityResult> SetInitialMasterPasswordAsync(
        User user,
        string masterPassword,
        string key,
        string orgSsoIdentifier
    );
}
