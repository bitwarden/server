using Bit.Core.Auth.UserFeatures.UserMasterPassword.Data;
using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Services;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword;

public class MasterPasswordService : IMasterPasswordService
{
    private readonly IUserService _userService;
    private readonly TimeProvider _timeProvider;
    private readonly ISetInitialMasterPasswordStateCommand _setInitialMasterPasswordStateCommand;
    private readonly IUpdateMasterPasswordStateCommand _updateMasterPasswordStateCommand;

    public MasterPasswordService(
        IUserService userService,
        TimeProvider timeProvider,
        ISetInitialMasterPasswordStateCommand setInitialMasterPasswordStateCommand,
        IUpdateMasterPasswordStateCommand updateMasterPasswordStateCommand)
    {
        _userService = userService;
        _timeProvider = timeProvider;
        _setInitialMasterPasswordStateCommand = setInitialMasterPasswordStateCommand;
        _updateMasterPasswordStateCommand = updateMasterPasswordStateCommand;
    }

    public async Task<IdentityResult> SetInitialMasterPasswordAsync(
        User user,
        SetInitialPasswordData setInitialData)
    {
        setInitialData.ValidateDataForUser(user);

        var result = await _userService.UpdatePasswordHash(
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

        var result = await _userService.UpdatePasswordHash(
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
}
