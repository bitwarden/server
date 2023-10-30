using Bit.Core.Entities;
using Bit.Core.Tools.Entities;
using Bit.Core.Vault.Entities;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.UserFeatures.UserKey.Interfaces;

/// <summary>
/// <para>TODO</para>
/// </summary>
public interface IRotateUserKeyCommand
{
    Task<IdentityResult> RotateUserKeyAsync(RotateUserKeyData model);
}
