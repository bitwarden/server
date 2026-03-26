using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
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

    public async Task<IdentityResult> SetInitialMasterPassword(User user, string masterPasswordHash, string key,
        KdfSettings kdf, string? salt = null, bool validatePassword = true, bool refreshStamp = true)
    {
        if (user.MasterPassword != null || user.Key != null)
        {
            throw new BadRequestException("User already has a master password set.");
        }

        var result = await _userService.UpdatePasswordHash(user, masterPasswordHash, validatePassword, refreshStamp);
        if (!result.Succeeded)
        {
            return result;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        user.Key = key;
        user.Kdf = kdf.KdfType;
        user.KdfIterations = kdf.Iterations;
        user.KdfMemory = kdf.Memory;
        user.KdfParallelism = kdf.Parallelism;
        user.LastPasswordChangeDate = now;
        user.RevisionDate = user.AccountRevisionDate = now;

        if (salt != null)
        {
            user.MasterPasswordSalt = salt;
        }

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> SetInitialMasterPasswordAsync(User user, string masterPasswordHash, string key,
        KdfSettings kdf, string? salt = null, bool validatePassword = true, bool refreshStamp = true)
    {
        var result = await SetInitialMasterPassword(user, masterPasswordHash, key, kdf, salt, validatePassword, refreshStamp);
        if (!result.Succeeded)
        {
            return result;
        }

        await _setInitialMasterPasswordStateCommand.ExecuteAsync(user);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateMasterPassword(User user, string masterPasswordHash, string key,
        KdfSettings kdf, string? salt = null, bool validatePassword = true, bool refreshStamp = true)
    {
        if (!user.HasMasterPassword())
        {
            throw new BadRequestException("User does not have an existing master password to update.");
        }

        kdf.ValidateUnchangedForUser(user);

        var result = await _userService.UpdatePasswordHash(user, masterPasswordHash, validatePassword, refreshStamp);
        if (!result.Succeeded)
        {
            return result;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        user.Key = key;
        user.LastPasswordChangeDate = now;
        user.RevisionDate = user.AccountRevisionDate = now;

        if (salt != null)
        {
            user.MasterPasswordSalt = salt;
        }

        return IdentityResult.Success;
    }

    public async Task<IdentityResult> UpdateMasterPasswordAsync(User user, string masterPasswordHash, string key,
        KdfSettings kdf, string? salt = null, bool validatePassword = true, bool refreshStamp = true)
    {
        var result = await UpdateMasterPassword(user, masterPasswordHash, key, kdf, salt, validatePassword, refreshStamp);
        if (!result.Succeeded)
        {
            return result;
        }

        await _updateMasterPasswordStateCommand.ExecuteAsync(user);
        return IdentityResult.Success;
    }
}
