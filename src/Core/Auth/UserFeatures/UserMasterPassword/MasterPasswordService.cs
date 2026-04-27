using Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using OneOf;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword;

internal class MasterPasswordService(
    IUserRepository userRepository,
    TimeProvider timeProvider,
    IPasswordHasher<User> passwordHasher,
    IEnumerable<IPasswordValidator<User>> passwordValidators,
    UserManager<User> userManager,
    ILogger<MasterPasswordService> logger)
    : IMasterPasswordService
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;
    private readonly IEnumerable<IPasswordValidator<User>> _passwordValidators = passwordValidators;
    private readonly UserManager<User> _userManager = userManager;
    private readonly ILogger<MasterPasswordService> _logger = logger;

    public async Task<OneOf<User, IdentityError[]>> PrepareSetInitialOrUpdateExistingMasterPasswordAsync(
        User user,
        SetInitialOrUpdateExistingPasswordData setOrUpdatePasswordData)
    {
        EnsureUserIsHydrated(user);

        if (user.HasMasterPassword())
        {
            return await PrepareUpdateExistingMasterPasswordAsync(
                user,
                setOrUpdatePasswordData.ToUpdateExistingData());
        }

        return await PrepareSetInitialMasterPasswordAsync(
            user,
            setOrUpdatePasswordData.ToSetInitialData());
    }

    public async Task<OneOf<User, IdentityError[]>> PrepareSetInitialMasterPasswordAsync(
        User user,
        SetInitialPasswordData setInitialData)
    {
        EnsureUserIsHydrated(user);
        setInitialData.ValidateDataForUser(user);

        // Note: Keep "unlock and authenticate" pattern in mind.
        // We name "unlock" first as a naming convention,
        // e.g., MasterPasswordUnlockAndAuthenticationDataModel. 
        // The two are complimentary, but mechanically Authenticate comes first.
        // Eager validation keeps the logic easier to reason about. 
        // Authentication is the mechanism for validation, unlock is the capability. 
        var result = await ApplyMasterPasswordAuthenticationHashAsync(
            user,
            setInitialData.MasterPasswordAuthentication.MasterPasswordAuthenticationHash,
            setInitialData.ValidatePassword);
        if (!result.Succeeded)
        {
            return result.Errors.ToArray();
        }

        if (setInitialData.RefreshStamp)
        {
            ApplyUserSecurityStampRotation(user);
        }

        user.Key = setInitialData.MasterPasswordUnlock.MasterKeyWrappedUserKey;
        ApplyKdfStateOnUser(user, setInitialData.MasterPasswordUnlock.Kdf);

        // Set salt on the user
        user.MasterPasswordSalt = setInitialData.MasterPasswordUnlock.Salt;

        // Always override the master password hint, even if it's null
        user.MasterPasswordHint = setInitialData.MasterPasswordHint;

        // Update time markers on the user
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        user.LastPasswordChangeDate = now;
        user.RevisionDate = user.AccountRevisionDate = now;

        return user;
    }

    public async Task<OneOf<User, IdentityError[]>> SaveSetInitialMasterPasswordAsync(
        User user,
        SetInitialPasswordData setInitialData)
    {
        EnsureUserIsHydrated(user);
        var result = await PrepareSetInitialMasterPasswordAsync(user, setInitialData);
        if (result.IsT1)
        {
            return result.AsT1;
        }

        await _userRepository.ReplaceAsync(result.AsT0);

        return result.AsT0;
    }

    public UpdateUserData BuildUpdateUserDelegateSetInitialMasterPassword(
        User user,
        SetInitialPasswordData setInitialData)
    {
        EnsureUserIsHydrated(user);
        setInitialData.ValidateDataForUser(user);

        return async (connection, transaction) =>
        {
            // Note: Keep "unlock and authenticate" pattern in mind.
            // We name "unlock" first as a naming convention,
            // e.g., MasterPasswordUnlockAndAuthenticationDataModel. 
            // The two are complimentary, but mechanically Authenticate comes first.
            // Eager validation keeps the logic easier to reason about. 
            // Authentication is the mechanism for validation, unlock is the capability. 
            if (setInitialData.ValidatePassword)
            {
                var validate = await ValidatePasswordInternalAsync(user,
                    setInitialData.MasterPasswordAuthentication.MasterPasswordAuthenticationHash);
                if (!validate.Succeeded)
                {
                    throw new BadRequestException(
                        string.Join("; ", validate.Errors.Select(e => e.Description)));
                }
            }

            if (setInitialData.RefreshStamp)
            {
                // TODO (PM-35501): IUserRepository.SetMasterPassword does not persist
                // SecurityStamp (sproc + EF query both omit it). Rotation here is
                // in-memory only until the primitive is extended.
                ApplyUserSecurityStampRotation(user);
            }

            // Hash the provided user master password authentication hash on the server side
            var serverSideHashedMasterPasswordAuthenticationHash = _passwordHasher.HashPassword(user,
                setInitialData.MasterPasswordAuthentication.MasterPasswordAuthenticationHash);

            await _userRepository.SetMasterPassword(user.Id,
                setInitialData.MasterPasswordUnlock, serverSideHashedMasterPasswordAuthenticationHash,
                setInitialData.MasterPasswordHint)(connection, transaction);
        };
    }

    public async Task<OneOf<User, IdentityError[]>> PrepareUpdateExistingMasterPasswordAsync(
        User user,
        UpdateExistingPasswordData updateExistingData)
    {
        EnsureUserIsHydrated(user);
        updateExistingData.ValidateDataForUser(user);

        // Note: Keep "unlock and authenticate" pattern in mind.
        // We name "unlock" first as a naming convention,
        // e.g., MasterPasswordUnlockAndAuthenticationDataModel. 
        // The two are complimentary, but mechanically Authenticate comes first.
        // Eager validation keeps the logic easier to reason about. 
        // Authentication is the mechanism for validation, unlock is the capability. 
        var result = await ApplyMasterPasswordAuthenticationHashAsync(
            user,
            updateExistingData.MasterPasswordAuthentication.MasterPasswordAuthenticationHash,
            updateExistingData.ValidatePassword);

        if (!result.Succeeded)
        {
            return result.Errors.ToArray();
        }

        if (updateExistingData.RefreshStamp)
        {
            ApplyUserSecurityStampRotation(user);
        }

        user.Key = updateExistingData.MasterPasswordUnlock.MasterKeyWrappedUserKey;

        // Always override the master password hint, even if it's null
        user.MasterPasswordHint = updateExistingData.MasterPasswordHint;

        // Update time markers on the user
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        user.LastPasswordChangeDate = now;
        user.RevisionDate = user.AccountRevisionDate = now;

        return user;
    }

    public async Task<OneOf<User, IdentityError[]>> SaveUpdateExistingMasterPasswordAndKdfAsync(
        User user,
        UpdateExistingPasswordAndKdfData updateExistingData)
    {
        EnsureUserIsHydrated(user);
        updateExistingData.ValidateDataForUser(user);

        // Note: Keep "unlock and authenticate" pattern in mind.
        // We name "unlock" first as a naming convention,
        // e.g., MasterPasswordUnlockAndAuthenticationDataModel. 
        // The two are complimentary, but mechanically Authenticate comes first.
        // Eager validation keeps the logic easier to reason about. 
        // Authentication is the mechanism for validation, unlock is the capability. 
        var result = await ApplyMasterPasswordAuthenticationHashAsync(
            user,
            updateExistingData.MasterPasswordAuthentication.MasterPasswordAuthenticationHash,
            updateExistingData.ValidatePassword);

        if (!result.Succeeded)
        {
            return result.Errors.ToArray();
        }

        if (updateExistingData.RefreshStamp)
        {
            ApplyUserSecurityStampRotation(user);
        }

        user.Key = updateExistingData.MasterPasswordUnlock.MasterKeyWrappedUserKey;
        ApplyKdfStateOnUser(user, updateExistingData.MasterPasswordUnlock.Kdf);

        // Always override the master password hint, even if it's null
        user.MasterPasswordHint = updateExistingData.MasterPasswordHint;

        // Update time markers on the user
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        user.LastPasswordChangeDate = now;
        user.LastKdfChangeDate = now;
        user.RevisionDate = user.AccountRevisionDate = now;

        await _userRepository.ReplaceAsync(user);

        return user;
    }

    public async Task<OneOf<User, IdentityError[]>> SaveUpdateExistingMasterPasswordAsync(
        User user,
        UpdateExistingPasswordData updateExistingData)
    {
        EnsureUserIsHydrated(user);
        var result = await PrepareUpdateExistingMasterPasswordAsync(user, updateExistingData);
        if (result.IsT1)
        {
            return result.AsT1;
        }

        await _userRepository.ReplaceAsync(result.AsT0);

        return result.AsT0;
    }

    ///<summary>
    ///Applies via hashing the <paramref name="newPassword"/> to the <paramref name="user"/>.
    ///Optionally validates the password, flagged with <paramref name="validatePassword"/>.
    ///Used by both initial-set and update master password paths.
    /// </summary>
    private async Task<IdentityResult> ApplyMasterPasswordAuthenticationHashAsync(User user, string newPassword,
        bool validatePassword = true)
    {
        if (validatePassword)
        {
            var validate = await ValidatePasswordInternalAsync(user, newPassword);
            if (!validate.Succeeded)
            {
                return validate;
            }
        }

        user.MasterPassword = _passwordHasher.HashPassword(user, newPassword);

        return IdentityResult.Success;
    }

    /// <summary>
    /// Rotates <see cref="Bit.Core.Entities.User.SecurityStamp"/> by replacing it with a new random value.
    /// <para>
    /// The security stamp is an opaque random identifier included as a claim in refresh tokens issued
    /// by IdentityServer. On every token refresh, the claim value is compared against the user's current
    /// stamp; a mismatch marks the token inactive and rejects the refresh. Rotating the stamp therefore
    /// immediately invalidates all active sessions without requiring a password change.
    /// </para>
    /// <para>
    /// Call this after any operation that changes the user's authentication credential or cryptographic
    /// state — setting or updating a master password hash, rotating KDF parameters — where session
    /// continuity is not intentionally preserved.
    /// </para>
    /// </summary>
    private static void ApplyUserSecurityStampRotation(User user)
    {
        user.SecurityStamp = Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Applies KDF parameters from the supplied <paramref name="kdf"/> to the <paramref name="user"/>.
    /// Used by both initial-set and KDF-rotation paths.
    /// </summary>
    private static void ApplyKdfStateOnUser(User user, KdfSettings kdf)
    {
        user.Kdf = kdf.KdfType;
        user.KdfIterations = kdf.Iterations;
        user.KdfMemory = kdf.Memory;
        user.KdfParallelism = kdf.Parallelism;
    }

    // A properly initialized or database-hydrated User should have at a minimum a non-default user ID.
    private static void EnsureUserIsHydrated(User user)
    {
        if (user.Id == default)
        {
            throw new ArgumentException("User must be hydrated with an assigned identity.", nameof(user));
        }
    }

    private async Task<IdentityResult> ValidatePasswordInternalAsync(User user, string password)
    {
        var errors = new List<IdentityError>();
        foreach (var v in _passwordValidators)
        {
            var result = await v.ValidateAsync(_userManager, user, password);
            if (!result.Succeeded)
            {
                errors.AddRange(result.Errors);
            }
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning("User {userId} password validation failed: {errors}.", user.Id,
                string.Join(";", errors.Select(e => e.Code)));
            return IdentityResult.Failed(errors.ToArray());
        }

        return IdentityResult.Success;
    }
}
