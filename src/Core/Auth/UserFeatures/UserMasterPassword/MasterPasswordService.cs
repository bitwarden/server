using Bit.Core.Auth.UserFeatures.UserMasterPassword.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.KeyManagement.Models.Data;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.Auth.UserFeatures.UserMasterPassword;

public class MasterPasswordService : IMasterPasswordService
{
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly TimeProvider _timeProvider;
    private readonly ISetInitialMasterPasswordStateCommand _setInitialMasterPasswordStateCommand;
    private readonly IUpdateMasterPasswordStateCommand _updateMasterPasswordStateCommand;

    public MasterPasswordService(
        IPasswordHasher<User> passwordHasher,
        TimeProvider timeProvider,
        ISetInitialMasterPasswordStateCommand setInitialMasterPasswordStateCommand,
        IUpdateMasterPasswordStateCommand updateMasterPasswordStateCommand)
    {
        _passwordHasher = passwordHasher;
        _timeProvider = timeProvider;
        _setInitialMasterPasswordStateCommand = setInitialMasterPasswordStateCommand;
        _updateMasterPasswordStateCommand = updateMasterPasswordStateCommand;
    }

    public void SetInitialMasterPassword(User user, string masterPasswordHash, string key, KdfSettings kdf, string? salt = null)
    {
        if (user.MasterPassword != null || user.Key != null)
        {
            throw new BadRequestException("User already has a master password set.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        user.MasterPassword = _passwordHasher.HashPassword(user, masterPasswordHash);
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
    }

    public async Task SetInitialMasterPasswordAsync(User user, string masterPasswordHash, string key, KdfSettings kdf, string? salt = null)
    {
        SetInitialMasterPassword(user, masterPasswordHash, key, kdf, salt);
        await _setInitialMasterPasswordStateCommand.ExecuteAsync(user);
    }

    public void UpdateMasterPassword(User user, string masterPasswordHash, string key, KdfSettings kdf, string? salt = null)
    {
        if (!user.HasMasterPassword())
        {
            throw new BadRequestException("User does not have an existing master password to update.");
        }

        kdf.ValidateUnchangedForUser(user);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        user.MasterPassword = _passwordHasher.HashPassword(user, masterPasswordHash);
        user.Key = key;
        user.LastPasswordChangeDate = now;
        user.RevisionDate = user.AccountRevisionDate = now;

        if (salt != null)
        {
            user.MasterPasswordSalt = salt;
        }
    }

    public async Task UpdateMasterPasswordAsync(User user, string masterPasswordHash, string key, KdfSettings kdf, string? salt = null)
    {
        UpdateMasterPassword(user, masterPasswordHash, key, kdf, salt);
        await _updateMasterPasswordStateCommand.ExecuteAsync(user);
    }
}
