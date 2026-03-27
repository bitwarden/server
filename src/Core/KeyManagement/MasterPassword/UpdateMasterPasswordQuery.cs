using Bit.Core.Entities;
using Bit.Core.KeyManagement.MasterPassword.Interfaces;
using Bit.Core.KeyManagement.Models.Data;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.KeyManagement.MasterPassword;

/// <inheritdoc />
public class UpdateMasterPasswordQuery : IUpdateMasterPasswordQuery
{
    private readonly IPasswordHasher<User> _passwordHasher;

    public UpdateMasterPasswordQuery(IPasswordHasher<User> passwordHasher)
    {
        _passwordHasher = passwordHasher;
    }

    /// <inheritdoc />
    public Task RunAsync(User user, UpdateMasterPasswordData data)
    {
        data.ValidateForUser(user);

        user.MasterPassword = _passwordHasher.HashPassword(user,
            data.MasterPasswordAuthentication.MasterPasswordAuthenticationHash);
        user.MasterPasswordHint = data.MasterPasswordHint;
        user.Key = data.MasterPasswordUnlock.MasterKeyWrappedUserKey;

        var now = DateTime.UtcNow;
        user.RevisionDate = now;
        user.AccountRevisionDate = now;
        user.LastPasswordChangeDate = now;

        return Task.CompletedTask;
    }
}
