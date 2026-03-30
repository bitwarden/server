using Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;


namespace Bit.Core.Auth.UserFeatures.UserMasterPassword;

public class MasterPasswordService : IMasterPasswordService
{
    private readonly TimeProvider _timeProvider;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IEnumerable<IPasswordValidator<User>> _passwordValidators;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<MasterPasswordService> _logger;
    private readonly ISetInitialMasterPasswordStateCommand _setInitialMasterPasswordStateCommand;
    private readonly IUpdateMasterPasswordStateCommand _updateMasterPasswordStateCommand;

    public MasterPasswordService(
        TimeProvider timeProvider,
        IPasswordHasher<User> passwordHasher,
        IEnumerable<IPasswordValidator<User>> passwordValidators,
        UserManager<User> userManager,
        ILogger<MasterPasswordService> logger,
        ISetInitialMasterPasswordStateCommand setInitialMasterPasswordStateCommand,
        IUpdateMasterPasswordStateCommand updateMasterPasswordStateCommand
        )
    {
        _timeProvider = timeProvider;
        _passwordHasher = passwordHasher;
        _passwordValidators = passwordValidators;
        _userManager = userManager;
        _logger = logger;
        _setInitialMasterPasswordStateCommand = setInitialMasterPasswordStateCommand;
        _updateMasterPasswordStateCommand = updateMasterPasswordStateCommand;
    }

    public async Task<IdentityResult> SetInitialMasterPasswordAsync(
        User user,
        SetInitialPasswordData setInitialData)
    {
        setInitialData.ValidateDataForUser(user);

        var result = await UpdatePasswordHash(
            user,
            setInitialData.MasterPasswordAuthenticationData.MasterPasswordAuthenticationHash,
            setInitialData.ValidatePassword,
            setInitialData.RefreshStamp);
        if (!result.Succeeded)
        {
            return result;
        }

        // Set kdf data on the user.
        user.Key = setInitialData.MasterPasswordUnlockData.MasterKeyWrappedUserKey;
        user.Kdf = setInitialData.MasterPasswordUnlockData.Kdf.KdfType;
        user.KdfIterations = setInitialData.MasterPasswordUnlockData.Kdf.Iterations;
        user.KdfMemory = setInitialData.MasterPasswordUnlockData.Kdf.Memory;
        user.KdfParallelism = setInitialData.MasterPasswordUnlockData.Kdf.Parallelism;

        // Set salt on the user
        user.MasterPasswordSalt = setInitialData.MasterPasswordUnlockData.Salt;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        user.LastPasswordChangeDate = now;
        user.RevisionDate = user.AccountRevisionDate = now;

        return IdentityResult.Success;
    }

    // Should this use IUserRepository.SetMasterPassword?
    public async Task<IdentityResult> SetInitialMasterPasswordAndSaveAsync(
        User user,
        SetInitialPasswordData setInitialData)
    {
        var result = await SetInitialMasterPasswordAsync(user, setInitialData);
        if (!result.Succeeded)
        {
            return result;
        }

        await _setInitialMasterPasswordStateCommand.ExecuteAsync(user);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateExistingMasterPasswordAsync(
        User user,
        UpdateExistingPasswordData updateExistingData)
    {
        updateExistingData.ValidateDataForUser(user);

        var result = await UpdatePasswordHash(
            user,
            updateExistingData.MasterPasswordAuthenticationData.MasterPasswordAuthenticationHash,
            updateExistingData.ValidatePassword,
            updateExistingData.RefreshStamp);
        if (!result.Succeeded)
        {
            return result;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        user.Key = updateExistingData.MasterPasswordUnlockData.MasterKeyWrappedUserKey;

        user.MasterPasswordSalt = updateExistingData.MasterPasswordUnlockData.Salt;

        user.LastPasswordChangeDate = now;
        user.RevisionDate = user.AccountRevisionDate = now;

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateExistingMasterPasswordAndSaveAsync(
        User user,
        UpdateExistingPasswordData updateExistingData)
    {
        var result = await UpdateExistingMasterPasswordAsync(user, updateExistingData);
        if (!result.Succeeded)
        {
            return result;
        }

        await _updateMasterPasswordStateCommand.ExecuteAsync(user);
        return IdentityResult.Success;
    }

    //
    private async Task<IdentityResult> UpdatePasswordHash(User user, string newPassword,
        bool validatePassword = true, bool refreshStamp = true)
    {
        if (validatePassword)
        {
            var validate = await ValidatePasswordInternal(user, newPassword);
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

    private async Task<IdentityResult> ValidatePasswordInternal(User user, string password)
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
