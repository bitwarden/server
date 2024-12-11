using Bit.Core.Entities;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.UserFeatures.TdeOffboardingPassword.Interfaces;

/// <summary>
/// <para>Manages the setting of the master password for JIT provisioned TDE <see cref="User"/> in an organization, after the organization disabled TDE.</para>
/// <para>This command is invoked, when the user first logs in after the organization has switched from TDE to master password based decryption.</para>
/// </summary>
public interface ITdeOffboardingPasswordCommand
{
    public Task<IdentityResult> UpdateTdeOffboardingPasswordAsync(
        User user,
        string masterPassword,
        string key,
        string orgSsoIdentifier
    );
}
