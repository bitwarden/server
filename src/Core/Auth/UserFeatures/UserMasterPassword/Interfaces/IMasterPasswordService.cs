using Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;

/// <summary>
/// This service bundles up all the ways we set an initial master password or update
/// an existing one into one place so we can perform the same validation and timestamp setting.
///
/// Meant to be used compositionally within other processes. Can be leveraged in controllers / commands / services.
/// Operations in here should be CRUD-like, not flow based logic with business logic.
///
/// There should never be business logic in this service. It is to bottleneck all flows that change and set
/// initial password so we can perform validation of the conditions while setting an initial password and when updating
/// an existing password.
/// </summary>
public interface IMasterPasswordService
{
    /// <summary>
    /// Inspects the user's current state and dispatches to either
    /// <see cref="MutateSetInitialMasterPasswordAsync"/> or
    /// <see cref="MutateUserUpdateExistingMasterPasswordAsync"/> accordingly.
    /// Mutates the <paramref name="user"/> object in memory only — no database write is performed.
    /// </summary>
    /// <param name="user">
    /// The user object to mutate. Whether the user already has a master password determines
    /// which code path executes.
    /// </param>
    /// <param name="setOrUpdatePasswordData">
    /// Combined cryptographic and authentication data that covers both the set-initial and
    /// update-existing paths. Converted internally via
    /// <see cref="SetInitialOrUpdateExistingPasswordData.ToSetInitialData"/> or
    /// <see cref="SetInitialOrUpdateExistingPasswordData.ToUpdateExistingData"/>.
    /// </param>
    /// <returns>
    /// <see cref="IdentityResult.Success"/> if the mutation succeeded; a failure result
    /// containing validation errors if <c>ValidatePassword</c> is set and the password
    /// fails the registered <see cref="Microsoft.AspNetCore.Identity.IPasswordValidator{TUser}"/> pipeline.
    /// </returns>
    Task<IdentityResult> MutateSetInitialPasswordOrUpdateExistingPassword(User user, SetInitialOrUpdateExistingPasswordData setOrUpdatePasswordData);

    /// <summary>
    /// Applies a new initial master password to the <paramref name="user"/> object in memory only —
    /// no database write is performed. Use when the caller controls persistence (e.g. key management
    /// flows that must compose this mutation with other transactional operations).
    /// </summary>
    /// <param name="user">
    /// The user object to mutate. Must not already have a master password; must have no existing
    /// <c>Key</c> or <c>MasterPasswordSalt</c>; must not be a Key Connector user.
    /// Validated via <see cref="SetInitialPasswordData.ValidateDataForUser"/>.
    /// </param>
    /// <param name="setInitialPasswordData">
    /// Cryptographic and authentication data required to set the initial password, including
    /// <c>MasterPasswordAuthentication</c> (hashed credential used for login),
    /// <c>MasterPasswordUnlock</c> (KDF parameters and wrapped user key),
    /// and control flags <c>ValidatePassword</c> and <c>RefreshStamp</c>.
    /// </param>
    /// <returns>
    /// <see cref="IdentityResult.Success"/> if the mutation succeeded; a failure result
    /// containing validation errors if <c>ValidatePassword</c> is set and the password
    /// fails the registered <see cref="Microsoft.AspNetCore.Identity.IPasswordValidator{TUser}"/> pipeline.
    /// </returns>
    Task<IdentityResult> MutateSetInitialMasterPasswordAsync(User user, SetInitialPasswordData setInitialPasswordData);

    /// <summary>
    /// Applies a new initial master password to the <paramref name="user"/> object and persists
    /// the updated user to the database. Use when no external transaction coordination is needed.
    /// </summary>
    /// <param name="user">
    /// The user object to mutate and persist. Subject to the same preconditions as
    /// <see cref="MutateSetInitialMasterPasswordAsync"/>.
    /// </param>
    /// <param name="setInitialPasswordData">
    /// Cryptographic and authentication data required to set the initial password. See
    /// <see cref="MutateSetInitialMasterPasswordAsync"/> for field details.
    /// </param>
    /// <returns>
    /// <see cref="IdentityResult.Success"/> if the mutation and save succeeded; a failure result
    /// containing validation errors if <c>ValidatePassword</c> is set and the password
    /// fails the registered <see cref="Microsoft.AspNetCore.Identity.IPasswordValidator{TUser}"/> pipeline.
    /// </returns>
    Task<IdentityResult> SaveSetInitialMasterPasswordAsync(User user, SetInitialPasswordData setInitialPasswordData);

    /// <summary>
    /// Returns a deferred database write (as an <see cref="UpdateUserData"/> delegate) for setting
    /// the initial master password. The delegate is intended to be passed to
    /// <see cref="IUserRepository.UpdateUserDataAsync"/>, which executes all supplied delegates
    /// within a single SQL transaction. Composing this delegate with others (e.g. cryptographic key
    /// writes) ensures every write succeeds or the entire batch rolls back atomically — a guarantee
    /// <see cref="SaveSetInitialMasterPasswordAsync"/> cannot provide on its own.
    /// <para>
    /// Note: despite the <c>Async</c> suffix, this method is synchronous — it constructs and returns
    /// the delegate without performing any I/O.
    /// </para>
    /// </summary>
    /// <param name="user">
    /// The user whose initial master password state will be written when the returned delegate is invoked.
    /// </param>
    /// <param name="setInitialPasswordData">
    /// Cryptographic and authentication data required to set the initial password. See
    /// <see cref="MutateSetInitialMasterPasswordAsync"/> for field details.
    /// </param>
    /// <returns>
    /// An <see cref="UpdateUserData"/> delegate suitable for inclusion in a batch passed to
    /// <see cref="IUserRepository.UpdateUserDataAsync"/>.
    /// </returns>
    UpdateUserData BuildTransactionSetInitialMasterPassword(User user, SetInitialPasswordData setInitialPasswordData);

    /// <summary>
    /// Applies a new master password over the user's existing one, mutating the
    /// <paramref name="user"/> object in memory only — no database write is performed.
    /// Use when the caller controls persistence.
    /// </summary>
    /// <param name="user">
    /// The user object to mutate. Will not update a master password salt. Must already have a master password;
    /// must not be a Key Connector user. KDF parameters and salt must be unchanged relative to the values in
    /// <paramref name="updateExistingData"/>. Validated via
    /// <see cref="UpdateExistingPasswordData.ValidateDataForUser"/>.
    /// </param>
    /// <param name="updateExistingData">
    /// Cryptographic and authentication data for the updated password, including
    /// <c>MasterPasswordAuthentication</c>, <c>MasterPasswordUnlock</c>,
    /// and control flags <c>ValidatePassword</c> and <c>RefreshStamp</c>.
    /// </param>
    /// <returns>
    /// <see cref="IdentityResult.Success"/> if the mutation succeeded; a failure result
    /// containing validation errors if <c>ValidatePassword</c> is set and the password
    /// fails the registered <see cref="Microsoft.AspNetCore.Identity.IPasswordValidator{TUser}"/> pipeline.
    /// </returns>
    Task<IdentityResult> MutateUserUpdateExistingMasterPasswordAsync(User user, UpdateExistingPasswordData updateExistingData);

    /// <summary>
    /// Applies a new master password over the user's existing one and persists the updated user
    /// to the database. Use when no external transaction coordination is needed.
    /// </summary>
    /// <param name="user">
    /// The user object to mutate and persist. Subject to the same preconditions as
    /// <see cref="MutateUserUpdateExistingMasterPasswordAsync"/>.
    /// </param>
    /// <param name="updateExistingData">
    /// Cryptographic and authentication data for the updated password. See
    /// <see cref="MutateUserUpdateExistingMasterPasswordAsync"/> for field details.
    /// </param>
    /// <returns>
    /// <see cref="IdentityResult.Success"/> if the mutation and save succeeded; a failure result
    /// containing validation errors if <c>ValidatePassword</c> is set and the password
    /// fails the registered <see cref="Microsoft.AspNetCore.Identity.IPasswordValidator{TUser}"/> pipeline.
    /// </returns>
    Task<IdentityResult> SaveUpdateExistingMasterPasswordAsync(User user, UpdateExistingPasswordData updateExistingData);
}
