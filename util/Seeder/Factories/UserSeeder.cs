using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.RustSDK;
using Microsoft.AspNetCore.Identity;

namespace Bit.Seeder.Factories;

internal static class UserSeeder
{
    internal const string DefaultPassword = "asdfasdfasdf";

    internal static User Create(
        string email,
        IPasswordHasher<User> passwordHasher,
        bool emailVerified = true,
        bool premium = false,
        UserKeys? keys = null)
    {
        keys ??= RustSdkService.GenerateUserKeys(email, DefaultPassword);

        var user = new User
        {
            Id = CoreHelpers.GenerateComb(),
            Email = email,
            EmailVerified = emailVerified,
            MasterPassword = null,
            SecurityStamp = Guid.NewGuid().ToString(),
            Key = keys.EncryptedUserKey,
            PublicKey = keys.PublicKey,
            PrivateKey = keys.PrivateKey,
            Premium = premium,
            ApiKey = Guid.NewGuid().ToString("N")[..30],
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 5_000,
        };

        user.MasterPassword = passwordHasher.HashPassword(user, keys.MasterPasswordHash);

        return user;
    }
}
