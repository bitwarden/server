using Bit.Core.Entities;
using Bit.Core.KeyManagement.MasterPassword.Interfaces;
using Bit.Core.KeyManagement.Models.Data;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.KeyManagement.MasterPassword;

/// <inheritdoc />
public class SetInitialMasterPasswordQuery : ISetInitialMasterPasswordQuery
{
    private readonly IPasswordHasher<User> _passwordHasher;

    public SetInitialMasterPasswordQuery(IPasswordHasher<User> passwordHasher)
    {
        _passwordHasher = passwordHasher;
    }

    /// <inheritdoc />
    public Task RunAsync(User user, SetInitialMasterPasswordData data)
    {
        data.ValidateForUser(user);

        user.MasterPassword = _passwordHasher.HashPassword(user,
            data.MasterPasswordAuthentication.MasterPasswordAuthenticationHash);
        user.MasterPasswordHint = data.MasterPasswordHint;
        user.MasterPasswordSalt = data.MasterPasswordAuthentication.Salt;
        user.Key = data.MasterPasswordUnlock.MasterKeyWrappedUserKey;
        user.Kdf = data.MasterPasswordAuthentication.Kdf.KdfType;
        user.KdfIterations = data.MasterPasswordAuthentication.Kdf.Iterations;
        user.KdfMemory = data.MasterPasswordAuthentication.Kdf.Memory;
        user.KdfParallelism = data.MasterPasswordAuthentication.Kdf.Parallelism;

        var now = DateTime.UtcNow;
        user.RevisionDate = now;
        user.AccountRevisionDate = now;
        user.LastPasswordChangeDate = now;

        return Task.CompletedTask;
    }
}
