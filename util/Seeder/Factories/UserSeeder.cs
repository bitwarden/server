using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.RustSDK;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Identity;

namespace Bit.Seeder.Factories;

internal static class UserSeeder
{
    internal const string DefaultPassword = "asdfasdfasdf";

    internal static User Create(
        string email,
        IPasswordHasher<User> passwordHasher,
        IManglerService manglerService,
        bool emailVerified = true,
        bool premium = false,
        UserKeys? keys = null,
        string? password = null)
    {
        // When keys are provided, caller owns email/key consistency - don't mangle
        var mangledEmail = keys == null ? manglerService.Mangle(email) : email;

        keys ??= RustSdkService.GenerateUserKeys(mangledEmail, password ?? DefaultPassword);

        var user = new User
        {
            Id = CoreHelpers.GenerateComb(),
            Email = mangledEmail,
            EmailVerified = emailVerified,
            MasterPassword = null,
            SecurityStamp = Guid.NewGuid().ToString(),
            Key = keys.EncryptedUserKey,
            PublicKey = keys.PublicKey,
            PrivateKey = keys.PrivateKey,
            Premium = premium,
            ApiKey = Guid.NewGuid().ToString("N")[..30],
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 5_000
        };

        user.MasterPassword = passwordHasher.HashPassword(user, keys.MasterPasswordHash);

        return user;
    }
}
