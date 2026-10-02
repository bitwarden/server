using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.RustSDK;
using Bit.Seeder.Models;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Identity;

namespace Bit.Seeder.Factories;

internal static class UserSeeder
{
    internal const string DefaultPassword = "asdfasdfasdf";

    internal static (User user, UserKeys keys) Create(
        UserSeed seed,
        IPasswordHasher<User> passwordHasher,
        IManglerService manglerService)
    {
        // When keys are provided, caller owns email/key consistency - don't mangle
        var keys = seed.Keys;
        var mangledEmail = keys == null ? manglerService.Mangle(seed.Email) : seed.Email;

        keys ??= RustSdkService.GenerateUserKeys(
            mangledEmail, seed.Password ?? DefaultPassword, seed.KdfIterations, seed.PoolIndex);

        var user = new User
        {
            Id = CombGuid.Generate(),
            Name = seed.Name ?? mangledEmail.Split('@')[0],
            Email = mangledEmail,
            EmailVerified = seed.EmailVerified,
            MasterPassword = null,
            MasterPasswordHint = seed.MasterPasswordHint,
            SecurityStamp = Guid.NewGuid().ToString(),
            Key = keys.EncryptedUserKey,
            PublicKey = keys.PublicKey,
            PrivateKey = keys.PrivateKey,
            Premium = seed.Premium,
            PremiumExpirationDate = seed.Premium ? DateTime.UtcNow.AddYears(1) : null,
            MaxStorageGb = seed.MaxStorageGb,
            Gateway = seed.Gateway,
            GatewayCustomerId = seed.GatewayCustomerId,
            GatewaySubscriptionId = seed.GatewaySubscriptionId,
            AvatarColor = seed.AvatarColor,
            ForcePasswordReset = seed.ForcePasswordReset,
            UsesKeyConnector = seed.UsesKeyConnector,
            ApiKey = CoreHelpers.SecureRandomString(30),
            Kdf = KdfType.PBKDF2_SHA256,
            KdfIterations = seed.KdfIterations
        };

        // Set only when supplied so the entity's own default ("en-US") survives.
        if (seed.Culture is not null)
        {
            user.Culture = seed.Culture;
        }

        if (seed.CreationDate is not null)
        {
            user.CreationDate = seed.CreationDate.Value;
        }

        if (seed.TwoFactorProviders is not null)
        {
            user.SetTwoFactorProviders(seed.TwoFactorProviders);
        }

        user.MasterPassword = passwordHasher.HashPassword(user, keys.MasterPasswordHash);

        return (user, keys);
    }
}
