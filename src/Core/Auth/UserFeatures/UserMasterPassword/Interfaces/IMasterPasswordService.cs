using Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Identity;
using OneOf;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;

/// <summary>
/// Centralized mutation point for all master password set, change, and rotate operations.
/// Provides consistent validation, password hashing, and timestamp management across every
/// flow that establishes or updates a user's master password.
///
/// <strong>Compositional, not orchestrating.</strong> This service handles CRUD-like mutations
/// only. Business logic (e.g., authorization checks, org validation, push notifications, event
/// logging) remains a caller responsibility.
///
/// <para><strong>Three persistence tiers:</strong></para>
/// <list type="bullet">
///   <item>
///     <c>Prepare*</c> Modifies the <see cref="User"/> object in memory only. The caller
///     controls when and how persistence occurs. Use when composing additional mutations before
///     saving (e.g. admin recovery flows that also clear 2FA or set <c>ForcePasswordReset</c>).
///     Returns <c>OneOf&lt;User, IdentityError[]&gt;</c>.
///   </item>
///   <item>
///     <c>Save*</c> Prepares the mutation and persists to the database via
///     <c>IUserRepository.ReplaceAsync</c>. Use for standalone operations where no
///     further mutation is needed. Returns <c>OneOf&lt;User, IdentityError[]&gt;</c>.
///   </item>
///   <item>
///     <c>Build*</c> Returns a deferred <see cref="UpdateUserData"/> delegate for
///     <see cref="IUserRepository.UpdateUserDataAsync"/> batch transactions. Use when the
///     password set is part of a larger transactional write that must succeed or fail atomically
///     (e.g. TDE key + password in a single SQL transaction)
///   </item>
/// </list>
///
/// <para><strong>Set vs Update contract:</strong></para>
/// <list type="bullet">
///   <item>
///     <strong>SET (initial):</strong> Client sends all data (hash, salt, KDF). Server sets all
///     fields. Stage 1 caveat: server enforces <c>salt == email.ToLowerInvariant().Trim()</c>
///     (PM-28143 removes this in Stage 3).
///   </item>
///   <item>
///     <strong>UPDATE (hash only):</strong> Client sends all data. Server validates KDF and salt
///     are unchanged, updates only the hash and wrapped user key.
///   </item>
///   <item>
///     <strong>UPDATE (KDF):</strong> Client sends all data. Server validates salt is unchanged,
///     updates hash, KDF, and wrapped user key.
///   </item>
/// </list>
///
/// <para><strong>Source of truth:</strong> On SET, the client is the source of truth. On UPDATE,
/// the server is the source of truth for fields that must not change — it validates the client's
/// values match what's stored before applying the update.</para>
/// </summary>
public interface IMasterPasswordService
{
    /// <summary>
    /// Inspects the user's current state and dispatches to either
    /// <see cref="PrepareSetInitialMasterPasswordAsync"/> or
    /// <see cref="PrepareUpdateExistingMasterPasswordAsync"/> accordingly.
    /// Prepares the <paramref name="user"/> object in memory only.
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
    /// On success, the modified <see cref="User"/>. On failure, an array of
    /// <see cref="IdentityError"/> describing validation failures.
    /// </returns>
    Task<OneOf<User, IdentityError[]>> PrepareSetInitialOrUpdateExistingMasterPasswordAsync(User user, SetInitialOrUpdateExistingPasswordData setOrUpdatePasswordData);

    /// <summary>
    /// Applies a new initial master password to the <paramref name="user"/> object in memory only. 
    /// Use for flows that must compose this mutation with other operations inside a larger transaction.
    /// </summary>
    /// <param name="user">
    /// The user object to mutate. Must not already have a master password; must have no existing
    /// <c>Key</c> or <c>MasterPasswordSalt</c>; must not be a Key Connector user.
    /// Validated via <see cref="SetInitialPasswordData.ValidateDataForUser"/>.
    /// </param>
    /// <param name="setInitialPasswordData">
    /// Cryptographic and authentication data required to set the initial password, including
    /// <c>MasterPasswordUnlock</c> (KDF parameters and wrapped user key),
    /// <c>MasterPasswordAuthentication</c> (hashed credential used for login),
    /// and control flags <c>ValidatePassword</c> and <c>RefreshStamp</c>.
    /// </param>
    /// <returns>
    /// On success, the modified <see cref="User"/>. On failure, an array of
    /// <see cref="IdentityError"/> describing validation failures.
    /// </returns>
    Task<OneOf<User, IdentityError[]>> PrepareSetInitialMasterPasswordAsync(User user, SetInitialPasswordData setInitialPasswordData);

    /// <summary>
    /// Note: This is to be used in the future when a TDE user wants to set a password with self-service.
    ///
    /// Applies a new initial master password to the <paramref name="user"/> object and persists
    /// the updated user. Use when no external transaction coordination is needed.
    /// </summary>
    /// <param name="user">
    /// The user object to mutate and persist. Subject to the same preconditions as
    /// <see cref="PrepareSetInitialMasterPasswordAsync"/>.
    /// </param>
    /// <param name="setInitialPasswordData">
    /// Cryptographic and authentication data required to set the initial password. See
    /// <see cref="PrepareSetInitialMasterPasswordAsync"/> for field details.
    /// </param>
    /// <returns>
    /// On success, the modified <see cref="User"/>. On failure, an array of
    /// <see cref="IdentityError"/> describing validation failures.
    /// </returns>
    Task<OneOf<User, IdentityError[]>> SaveSetInitialMasterPasswordAsync(User user, SetInitialPasswordData setInitialPasswordData);

    /// <summary>
    /// Returns a deferred database write (as an <see cref="UpdateUserData"/> delegate) for setting
    /// the initial master password. The delegate is intended to be passed to
    /// <see cref="IUserRepository.UpdateUserDataAsync"/>, which executes all supplied delegates
    /// within a single SQL transaction. Composing this delegate with others (e.g. cryptographic key
    /// writes) ensures every write succeeds or the entire batch rolls back atomically, a guarantee
    /// <see cref="SaveSetInitialMasterPasswordAsync"/> cannot provide on its own.
    /// </summary>
    /// <param name="user">
    /// The user whose initial master password state will be written when the returned delegate is invoked.
    /// </param>
    /// <param name="setInitialPasswordData">
    /// Cryptographic and authentication data required to set the initial password. See
    /// <see cref="PrepareSetInitialMasterPasswordAsync"/> for field details.
    /// </param>
    /// <returns>
    /// An <see cref="UpdateUserData"/> delegate suitable for inclusion in a batch passed to
    /// <see cref="IUserRepository.UpdateUserDataAsync"/>.
    /// </returns>
    UpdateUserData BuildUpdateUserDelegateSetInitialMasterPassword(User user, SetInitialPasswordData setInitialPasswordData);

    /// <summary>
    /// Applies a new master password over the user's existing one, mutating the
    /// <paramref name="user"/> object in memory only.
    /// Use for flows that must compose this mutation with other operations inside a larger transaction.
    /// </summary>
    /// <param name="user">
    /// The user object to mutate. Must already have a master password;
    /// must not be a Key Connector user. KDF parameters and salt must be unchanged relative to the values in
    /// <paramref name="updateExistingData"/>. Validated via
    /// <see cref="UpdateExistingPasswordData.ValidateDataForUser"/>.
    /// </param>
    /// <param name="updateExistingData">
    /// Cryptographic and authentication data for the updated password, including
    /// <c>MasterPasswordUnlock</c>, <c>MasterPasswordAuthentication</c>,
    /// and control flags <c>ValidatePassword</c> and <c>RefreshStamp</c>.
    /// </param>
    /// <returns>
    /// On success, the modified <see cref="User"/>. On failure, an array of
    /// <see cref="IdentityError"/> describing validation failures.
    /// </returns>
    Task<OneOf<User, IdentityError[]>> PrepareUpdateExistingMasterPasswordAsync(User user, UpdateExistingPasswordData updateExistingData);

    /// <summary>
    /// Applies a new master password and updated KDF parameters over the user's existing ones
    /// and persists the updated user to the database. Salt must remain unchanged; KDF is
    /// intentionally allowed to change. Use for KDF rotation flows.
    /// </summary>
    /// <param name="user">
    /// The user object to mutate and persist. Must already have a master password;
    /// must not be a Key Connector user. Salt must be unchanged. Validated via
    /// <see cref="UpdateExistingPasswordAndKdfData.ValidateDataForUser"/>.
    /// </param>
    /// <param name="updateExistingExistingData">
    /// Cryptographic and authentication data for the updated password and KDF parameters,
    /// including <c>MasterPasswordUnlock</c>, <c>MasterPasswordAuthentication</c>,
    /// and control flags <c>ValidatePassword</c> and <c>RefreshStamp</c>.
    /// </param>
    /// <returns>
    /// On success, the modified <see cref="User"/>. On failure, an array of
    /// <see cref="IdentityError"/> describing validation failures.
    /// </returns>
    Task<OneOf<User, IdentityError[]>> SaveUpdateExistingMasterPasswordAndKdfAsync(User user, UpdateExistingPasswordAndKdfData updateExistingExistingData);

    /// <summary>
    /// Applies a new master password over the user's existing one and persists the updated user
    /// to the database. Use when no external transaction coordination is needed.
    /// </summary>
    /// <param name="user">
    /// The user object to mutate and persist. Subject to the same preconditions as
    /// <see cref="PrepareUpdateExistingMasterPasswordAsync"/>.
    /// </param>
    /// <param name="updateExistingData">
    /// Cryptographic and authentication data for the updated password. See
    /// <see cref="PrepareUpdateExistingMasterPasswordAsync"/> for field details.
    /// </param>
    /// <returns>
    /// On success, the modified <see cref="User"/>. On failure, an array of
    /// <see cref="IdentityError"/> describing validation failures.
    /// </returns>
    Task<OneOf<User, IdentityError[]>> SaveUpdateExistingMasterPasswordAsync(User user, UpdateExistingPasswordData updateExistingData);
}
