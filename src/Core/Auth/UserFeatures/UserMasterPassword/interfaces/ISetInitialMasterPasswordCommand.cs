using Bit.Core.Entities;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;

public interface ISetInitialMasterPasswordCommand
{
    public Task<IdentityResult> SetInitialMasterPasswordAsync(User user, string masterPassword, string key,
        string orgIdentifier = null);
}
