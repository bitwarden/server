using Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Identity;
using OneOf;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;

/// <summary>
/// Shared service for all master password set and update operations.
/// Provides consistent validation, password hashing, and timestamp management across every
/// flow that establishes or updates a user's master password.
///
/// Compositional, not orchestrating. This service handles CRUD-like mutations and/or persistence
/// only. Business logic (e.g., authorization checks, org validation, push notifications, event
/// logging) remains a caller responsibility.
///
/// <para>
/// What our verbs do:
/// This surface has a 3-verb API.
/// Choose between Prepare, Save, or Build; each has a consistent mutation/persistence
/// opinion.
/// </para>
/// <list type="bullet">
///   <item>
///     <term>Prepare</term> Modifies the <see cref="User"/> object in memory only. The caller
///     controls when and how persistence occurs. Use when composing mutations before
///     saving (e.g. admin recovery flows that also clear 2FA or set <see cref="User.ForcePasswordReset"/>).
///     Returns <see cref="OneOf{T0, T1}"/> of <see cref="User"/>, array of <see cref="IdentityError"/>.
///   </item>
///   <item>
///     <term>Save</term> Prepares the mutation and persists to the database via
///     <see cref="Bit.Core.Repositories.IRepository{T, TId}.ReplaceAsync"/>. Use for standalone operations where no
///     further mutation is needed.
///     Returns <see cref="OneOf{T0, T1}"/> of <see cref="User"/>, array of <see cref="IdentityError"/>.
///   </item>
///   <item>
///     <term>Build</term> Returns a deferred <see cref="UpdateUserData"/> delegate for
///     <see cref="IUserRepository.UpdateUserDataAsync"/> batch transactions. Use when the
///     password set is part of a larger transactional write that must succeed or fail atomically
///     (e.g. TDE key + password in a single SQL transaction)
///   </item>
/// </list>
///
/// <para>
/// When to choose an operation:
/// Choose between Set (initial/new) and Update (existing); each has purpose-made validation
/// and consistency constraints.
/// </para>
/// <list type="bullet">
///   <item>
///     <term>Set</term>
///     <description>Client sends all data (hash, salt, KDF). Server sets all
///     fields. Stage 1 caveat: server enforces <c>salt == email.ToLowerInvariant().Trim()</c>
///     (PM-28143 removes this in Stage 3). Use when creating a new user.</description>
///   </item>
///   <item>
///     <term>Update (hash only)</term>
///     <description>Client sends all data. Server validates KDF and salt
///     are unchanged, updates only the hash and wrapped user key. Use when
///     updating master password.</description>
///   </item>
///   <item>
///     <term>Update (KDF)</term>
///     <description>Client sends all data. Server validates salt is unchanged.
///     Use when updating KDF settings and master key-wrapped user key.</description>
///   </item>
/// </list>
///
/// <para>Source of truth: On Set, the client is the source of truth. On Update,
/// the server is the source of truth for fields that must not change — it validates the client's
/// values match what's stored before applying the update.</para>
/// </summary>
internal interface IMasterPasswordService
{
    /// <summary>
    /// Inspects the user's current state and dispatches to either
    /// <see cref="PrepareSetInitialMasterPasswordAsync"/> or
    /// <see cref="PrepareUpdateExistingMasterPasswordAsync"/> accordingly.
    /// Prepares the <paramref name="user"/> object in memory only.
    ///
    /// <para>
    /// Use when: composing a set-initial or update-existing mutation with other operations
    /// before saving, and the caller does not need to select the path explicitly — dispatch
    /// is determined by the user's current master password state.
    /// </para>
    ///
    /// <para>
    /// Constraints: Delegated to the dispatched method; see
    /// <see cref="PrepareSetInitialMasterPasswordAsync"/> or
    /// <see cref="PrepareUpdateExistingMasterPasswordAsync"/>.
    /// </para>
    ///
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
    /// 
    /// <para>
    /// Use when: composing this mutation with other operations inside a larger transaction.
    /// </para>
    /// 
    /// <para>
    /// Constraints:
    /// <list type="bullet">
    ///   <item>User must not already have a master password.</item>
    ///   <item>User must have no existing <c>Key</c> or <c>MasterPasswordSalt</c>.</item>
    ///   <item>User must not be a Key Connector user.</item>
    /// </list>
    /// </para>
    /// 
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
    /// Applies a new initial master password to the <paramref name="user"/> object and persists
    /// the updated user.
    ///
    /// <para>
    /// Use when: a TDE user wants to set a password via self-service and no external transaction
    /// coordination is needed. Note: intended for future use.
    /// </para>
    ///
    /// <para>
    /// Constraints (see also <see cref="PrepareSetInitialMasterPasswordAsync"/>):
    /// <list type="bullet">
    ///   <item>User must not already have a master password.</item>
    ///   <item>User must have no existing <c>Key</c> or <c>MasterPasswordSalt</c>.</item>
    ///   <item>User must not be a Key Connector user.</item>
    /// </list>
    /// </para>
    ///
    /// </summary>
    /// <param name="user">
    /// The user object to mutate and persist. Must not already have a master password; must have no
    /// existing <c>Key</c> or <c>MasterPasswordSalt</c>; must not be a Key Connector user.
    /// Validated via <see cref="SetInitialPasswordData.ValidateDataForUser"/>.
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
    /// within a single SQL transaction.
    ///
    /// <para>
    /// Use when: the password set is part of a larger transactional write that must succeed or fail
    /// atomically (e.g., TDE key + password in a single SQL transaction). Composing this delegate
    /// with others ensures every write succeeds or the entire batch rolls back, a guarantee
    /// <see cref="SaveSetInitialMasterPasswordAsync"/> cannot provide on its own.
    /// </para>
    ///
    /// <para>
    /// Constraints (see also <see cref="PrepareSetInitialMasterPasswordAsync"/>):
    /// <list type="bullet">
    ///   <item>User must not already have a master password.</item>
    ///   <item>User must have no existing <c>Key</c> or <c>MasterPasswordSalt</c>.</item>
    ///   <item>User must not be a Key Connector user.</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// NOTE: This exists to support an existing pattern. Long-term preference is to be able to sunset this method
    /// and lean on Prepare and Save verbs only. Please prefer those verbs if possible.
    /// </para>
    ///
    /// </summary>
    /// <param name="user">
    /// The user whose initial master password state will be written when the returned delegate is
    /// invoked. Must not already have a master password; must have no existing <c>Key</c> or
    /// <c>MasterPasswordSalt</c>; must not be a Key Connector user.
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
    ///
    /// <para>
    /// Use when: composing this mutation with other operations inside a larger transaction.
    /// </para>
    ///
    /// <para>
    /// Constraints:
    /// <list type="bullet">
    ///   <item>User must already have a master password.</item>
    ///   <item>User must not be a Key Connector user.</item>
    ///   <item>KDF parameters and salt must be unchanged relative to the values in <paramref name="updateExistingData"/>.</item>
    /// </list>
    /// </para>
    ///
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
    /// and persists the updated user to the database. KDF validation is intentionally skipped.
    ///
    /// <para>
    /// Use when: rotating KDF parameters.
    /// </para>
    ///
    /// <para>
    /// Constraints:
    /// <list type="bullet">
    ///   <item>User must already have a master password.</item>
    ///   <item>User must not be a Key Connector user.</item>
    ///   <item>Salt must be unchanged.</item>
    /// </list>
    /// </para>
    ///
    /// </summary>
    /// <param name="user">
    /// The user object to mutate and persist. Must already have a master password;
    /// must not be a Key Connector user. Salt must be unchanged. Validated via
    /// <see cref="UpdateExistingPasswordAndKdfData.ValidateDataForUser"/>.
    /// </param>
    /// <param name="updateExistingData">
    /// Cryptographic and authentication data for the updated password and KDF parameters,
    /// including <c>MasterPasswordUnlock</c>, <c>MasterPasswordAuthentication</c>,
    /// and control flags <c>ValidatePassword</c> and <c>RefreshStamp</c>.
    /// </param>
    /// <returns>
    /// On success, the modified <see cref="User"/>. On failure, an array of
    /// <see cref="IdentityError"/> describing validation failures.
    /// </returns>
    Task<OneOf<User, IdentityError[]>> SaveUpdateExistingMasterPasswordAndKdfAsync(User user, UpdateExistingPasswordAndKdfData updateExistingData);

    /// <summary>
    /// Applies a new master password over the user's existing one and persists the updated user
    /// to the database.
    ///
    /// <para>
    /// Use when: no external transaction coordination is needed.
    /// </para>
    ///
    /// <para>
    /// Constraints (see also <see cref="PrepareUpdateExistingMasterPasswordAsync"/>):
    /// <list type="bullet">
    ///   <item>User must already have a master password.</item>
    ///   <item>User must not be a Key Connector user.</item>
    ///   <item>KDF parameters and salt must be unchanged relative to the values in <paramref name="updateExistingData"/>.</item>
    /// </list>
    /// </para>
    ///
    /// </summary>
    /// <param name="user">
    /// The user object to mutate and persist. Must already have a master password;
    /// must not be a Key Connector user. KDF parameters and salt must be unchanged relative to the
    /// values in <paramref name="updateExistingData"/>. Validated via
    /// <see cref="UpdateExistingPasswordData.ValidateDataForUser"/>.
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
