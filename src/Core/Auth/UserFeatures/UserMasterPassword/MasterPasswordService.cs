using Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword;

public class MasterPasswordService(
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

    public async Task<IdentityResult> OnlyMutateEitherUpdateExistingPasswordOrSetInitialPassword(
        User user,
        SetInitialOrChangeExistingPasswordData setOrUpdatePasswordData)
    {
        IdentityResult mutationResult;
        if (user.HasMasterPassword())
        {
            mutationResult = await OnlyMutateUserUpdateExistingMasterPasswordAsync(
                user,
                setOrUpdatePasswordData.ToUpdateExistingData());
        }
        else
        {
            mutationResult = await OnlyMutateUserSetInitialMasterPasswordAsync(
                user,
                setOrUpdatePasswordData.ToSetInitialData());
        }

        return mutationResult;
    }

    public async Task<IdentityResult> OnlyMutateUserSetInitialMasterPasswordAsync(
        User user,
        SetInitialPasswordData setInitialData)
    {
        setInitialData.ValidateDataForUser(user);

        var result = await UpdateExistingPasswordHashAsync(
            user,
            setInitialData.MasterPasswordAuthentication.MasterPasswordAuthenticationHash,
            setInitialData.ValidatePassword,
            setInitialData.RefreshStamp);
        if (!result.Succeeded)
        {
            return result;
        }

        // Set kdf data on the user
        user.Key = setInitialData.MasterPasswordUnlock.MasterKeyWrappedUserKey;
        user.Kdf = setInitialData.MasterPasswordUnlock.Kdf.KdfType;
        user.KdfIterations = setInitialData.MasterPasswordUnlock.Kdf.Iterations;
        user.KdfMemory = setInitialData.MasterPasswordUnlock.Kdf.Memory;
        user.KdfParallelism = setInitialData.MasterPasswordUnlock.Kdf.Parallelism;

        // Set salt on the user
        user.MasterPasswordSalt = setInitialData.MasterPasswordUnlock.Salt;

        // Always override the master password hint, even if it's null.
        user.MasterPasswordHint = setInitialData.MasterPasswordHint;

        // Update time markers on the user
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        user.LastPasswordChangeDate = now;
        user.RevisionDate = user.AccountRevisionDate = now;

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> SetInitialMasterPasswordAndSaveUserAsync(
        User user,
        SetInitialPasswordData setInitialData)
    {
        // No need to validate because we will validate in the sibling call here
        var result = await OnlyMutateUserSetInitialMasterPasswordAsync(user, setInitialData);
        if (!result.Succeeded)
        {
            return result;
        }

        await _userRepository.ReplaceAsync(user);

        return IdentityResult.Success;
    }

    public UpdateUserData BuildTransactionForSetInitialMasterPassword(
        User user,
        SetInitialPasswordData setInitialData)
    {
        setInitialData.ValidateDataForUser(user);

        // Hash the provided user master password authentication hash on the server side
        var serverSideHashedMasterPasswordAuthenticationHash = _passwordHasher.HashPassword(user,
            setInitialData.MasterPasswordAuthentication.MasterPasswordAuthenticationHash);

        var setMasterPasswordTask = _userRepository.SetMasterPassword(user.Id,
            setInitialData.MasterPasswordUnlock, serverSideHashedMasterPasswordAuthenticationHash,
            setInitialData.MasterPasswordHint);

        return setMasterPasswordTask;
    }

    public async Task<IdentityResult> OnlyMutateUserUpdateExistingMasterPasswordAsync(
        User user,
        UpdateExistingPasswordData updateExistingData)
    {
        // Start by validating the update payload
        updateExistingData.ValidateDataForUser(user);

        var result = await UpdateExistingPasswordHashAsync(
            user,
            updateExistingData.MasterPasswordAuthentication.MasterPasswordAuthenticationHash,
            updateExistingData.ValidatePassword,
            updateExistingData.RefreshStamp);

        if (!result.Succeeded)
        {
            return result;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        user.Key = updateExistingData.MasterPasswordUnlock.MasterKeyWrappedUserKey;

        // Always override the master password hint, even if it's null.
        user.MasterPasswordHint = updateExistingData.MasterPasswordHint;

        user.LastPasswordChangeDate = now;
        user.RevisionDate = user.AccountRevisionDate = now;

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateExistingMasterPasswordAndSaveAsync(
        User user,
        UpdateExistingPasswordData updateExistingData)
    {
        // No need to validate because we will validate in the sibling call here.
        var result = await OnlyMutateUserUpdateExistingMasterPasswordAsync(user, updateExistingData);
        if (!result.Succeeded)
        {
            return result;
        }

        await _userRepository.ReplaceAsync(user);

        return IdentityResult.Success;
    }

    private async Task<IdentityResult> UpdateExistingPasswordHashAsync(User user, string newPassword,
        bool validatePassword = true, bool refreshStamp = true)
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
        if (refreshStamp)
        {
            user.SecurityStamp = Guid.NewGuid().ToString();
        }

        return IdentityResult.Success;
    }

    // Taken from the
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
