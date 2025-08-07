using Bit.Core.Enums;
using Bit.Infrastructure.EntityFramework.Models;
using Bit.RustSDK;
using Microsoft.AspNetCore.Identity;

namespace Bit.Seeder.Factories;

public class UserSeeder
{

    public static (User user, string userKey) CreateUser(IPasswordHasher<Bit.Core.Entities.User> passwordHasher, string email)
    {
        var nativeService = RustSdkServiceFactory.CreateSingleton();
        var keys = nativeService.GenerateUserKeys(email, "asdfasdfasdf");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            MasterPassword = null,
            SecurityStamp = "4830e359-e150-4eae-be2a-996c81c5e609",
            Key = keys.EncryptedUserKey,
            PublicKey = keys.PublicKey,
            PrivateKey = keys.PrivateKey,
            ApiKey = "7gp59kKHt9kMlks0BuNC4IjNXYkljR",

            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 5_000,
        };

        user.MasterPassword = passwordHasher.HashPassword(user, keys.MasterPasswordHash);

        return (user, keys.Key);
    }
}
