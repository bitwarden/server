using Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;

/// <summary>
/// This service bundles up all the ways we set an initial master password or update
/// an existing one into one place so we can perform the same
/// </summary>
public interface IMasterPasswordService
{
    Task<IdentityResult> OnlyMutateEitherUpdateExistingPasswordOrSetInitialPassword(User user, SetInitialPasswordData setInitialPasswordData, UpdateExistingPasswordData updateExistingData);

    /// <summary>
    /// To be used when you only want to mutate the user object but not perform a write to the database.
    /// </summary>
    /// <param name="user"></param>
    /// <param name="setInitialPasswordData"></param>
    /// <returns></returns>
    Task<IdentityResult> OnlyMutateUserSetInitialMasterPasswordAsync(User user, SetInitialPasswordData setInitialPasswordData);

    /// <summary>
    /// To be used when you only want to mutate the user object and perform a write to the database of the updated user.
    /// </summary>
    /// <param name="user"></param>
    /// <param name="setInitialPasswordData"></param>
    /// <returns></returns>
    Task<IdentityResult> SetInitialMasterPasswordAndSaveUserAsync(User user, SetInitialPasswordData setInitialPasswordData);

    /// <summary>
    /// Key management needs to couple cryptographic operations along with the set password operation that guarantees
    /// rollback if the operation is a failure. So we need a function to specifically build the operation to set the
    /// password.
    /// </summary>
    /// <param name="user"></param>
    /// <param name="setInitialPasswordData"></param>
    /// <returns></returns>
    UpdateUserData BuildTransactionForSetInitialMasterPasswordAsync(User user, SetInitialPasswordData setInitialPasswordData);

    Task<IdentityResult> OnlyMutateUserUpdateExistingMasterPasswordAsync(User user, UpdateExistingPasswordData updateExistingData);

    Task<IdentityResult> UpdateExistingMasterPasswordAndSaveAsync(User user, UpdateExistingPasswordData updateExistingData);
}
