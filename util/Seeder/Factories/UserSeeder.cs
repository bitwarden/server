using System.Globalization;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.RustSDK;
using Microsoft.AspNetCore.Identity;

namespace Bit.Seeder.Factories;

public struct UserData
{
    public string Email;
    public Guid Id;
    public string? Key;
    public string? PublicKey;
    public string? PrivateKey;
    public string? ApiKey;
    public KdfType Kdf;
    public int KdfIterations;
}

public class UserSeeder(RustSdkService sdkService, IPasswordHasher<Bit.Core.Entities.User> passwordHasher, MangleId mangleId)
{
    private string MangleEmail(string email)
    {
        return $"{mangleId}+{email}";
    }

    public User CreateUser(string email, bool emailVerified = false, bool premium = false)
    {
        email = MangleEmail(email);
        var keys = sdkService.GenerateUserKeys(email, DefaultPassword);

        var user = new User
        {
            Id = CoreHelpers.GenerateComb(),
            Email = email,
            EmailVerified = emailVerified,
            MasterPassword = null,
            SecurityStamp = "4830e359-e150-4eae-be2a-996c81c5e609",
            Key = keys.EncryptedUserKey,
            PublicKey = keys.PublicKey,
            PrivateKey = keys.PrivateKey,
            Premium = premium,
            ApiKey = "7gp59kKHt9kMlks0BuNC4IjNXYkljR",
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 5_000,
        };

        user.MasterPassword = passwordHasher.HashPassword(user, keys.MasterPasswordHash);

        return user;
    }

    /// <summary>
    /// Default test password used for all seeded users.
    /// </summary>
    public const string DefaultPassword = "asdfasdfasdf";

    /// <summary>
    /// Creates a user with SDK-generated cryptographic keys (no email mangling).
    /// The user can log in with email and password = "asdfasdfasdf".
    /// </summary>
    public static User CreateUserWithSdkKeys(
        string email,
        RustSdkService sdkService,
        IPasswordHasher<User> passwordHasher)
    {
        var keys = sdkService.GenerateUserKeys(email, DefaultPassword);

        var user = new User
        {
            Id = CoreHelpers.GenerateComb(),
            Email = email,
            EmailVerified = true,
            MasterPassword = null,
            SecurityStamp = Guid.NewGuid().ToString(),
            Key = keys.EncryptedUserKey,
            PublicKey = keys.PublicKey,
            PrivateKey = keys.PrivateKey,
            Premium = false,
            ApiKey = Guid.NewGuid().ToString("N")[..30],
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = 5_000,
        };

        user.MasterPassword = passwordHasher.HashPassword(user, keys.MasterPasswordHash);

        return user;
    }

    public Dictionary<string, string?> GetMangleMap(User user, UserData expectedUserData)
    {
        var mangleMap = new Dictionary<string, string?>
        {
            { expectedUserData.Email, MangleEmail(expectedUserData.Email) },
            { expectedUserData.Id.ToString(), user.Id.ToString() },
            { expectedUserData.Kdf.ToString(), user.Kdf.ToString() },
            { expectedUserData.KdfIterations.ToString(CultureInfo.InvariantCulture), user.KdfIterations.ToString(CultureInfo.InvariantCulture) }
        };
        if (expectedUserData.Key != null)
        {
            mangleMap[expectedUserData.Key] = user.Key;
        }

        if (expectedUserData.PublicKey != null)
        {
            mangleMap[expectedUserData.PublicKey] = user.PublicKey;
        }

        if (expectedUserData.PrivateKey != null)
        {
            mangleMap[expectedUserData.PrivateKey] = user.PrivateKey;
        }

        if (expectedUserData.ApiKey != null)
        {
            mangleMap[expectedUserData.ApiKey] = user.ApiKey;
        }

        return mangleMap;
    }
}
