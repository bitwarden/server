using Bit.Core.Entities;
using Bitwarden.Server.Sdk.PasswordHashing;
using Microsoft.AspNetCore.Identity;

namespace Bit.Core.KeyManagement.PasswordHashing;

/// <summary>
/// ASP.NET Identity's user password hasher, backed by <see cref="IServerPasswordHasher"/>, so
/// UserManager and every master password caller hash with the Key Management package.
/// </summary>
internal sealed class UserPasswordHasher(IServerPasswordHasher hasher) : IPasswordHasher<User>
{
    public string HashPassword(User user, string password) =>
        hasher.CreateStoredHash(new ClientCreatedHash(password)).Value;

    public PasswordVerificationResult VerifyHashedPassword(User user, string hashedPassword, string providedPassword) =>
        hasher.VerifyStoredHash(new ServerStorableHash(hashedPassword), new ClientCreatedHash(providedPassword)) switch
        {
            PasswordHashVerification.Success => PasswordVerificationResult.Success,
            PasswordHashVerification.SuccessRehashNeeded => PasswordVerificationResult.SuccessRehashNeeded,
            _ => PasswordVerificationResult.Failed,
        };
}
