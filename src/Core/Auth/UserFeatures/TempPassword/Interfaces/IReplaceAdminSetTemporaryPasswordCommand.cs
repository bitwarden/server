using Bit.Core.Entities;
using Bit.Core.KeyManagement.Models.Data;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.UserFeatures.TempPassword.Interfaces;

public interface IReplaceAdminSetTemporaryPasswordCommand
{
    Task<IdentityResult> Replace(
        User user,
        MasterPasswordUnlockData unlockData,
        MasterPasswordAuthenticationData authenticationData,
        string? masterPasswordHint);
}
