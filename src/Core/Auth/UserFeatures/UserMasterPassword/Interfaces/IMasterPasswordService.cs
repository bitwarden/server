using Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;
using Bit.Core.Entities;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;

/// <summary>
/// This service defines the correct way to set an initial or update an existing password
/// for a user.
/// </summary>
public interface IMasterPasswordService
{
    Task<IdentityResult> SetInitialMasterPasswordAsync(User user, SetInitialPasswordData setInitialPasswordData);

    Task<IdentityResult> SetInitialMasterPasswordAndSaveAsync(User user, SetInitialPasswordData setInitialPasswordData);

    Task<IdentityResult> UpdateExistingMasterPasswordAsync(User user, UpdateExistingPasswordData updateExistingData);

    Task<IdentityResult> UpdateExistingMasterPasswordAndSaveAsync(User user, UpdateExistingPasswordData updateExistingData);
}
