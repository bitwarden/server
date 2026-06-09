using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Bit.Core.Billing.Models.Business;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Identity;

namespace Bit.Seeder.Scenes;

public struct SingleUserSceneResult
{
    public Guid UserId { get; init; }
    public string Kdf { get; init; }
    public int KdfIterations { get; init; }
    public string Key { get; init; }
    public string DecryptedKeyB64 { get; init; }
    public string PublicKey { get; init; }
    public string PrivateKey { get; init; }
    public string ApiKey { get; init; }
}

/// <summary>
/// Creates a single user using the provided account details.
/// </summary>
public class SingleUserScene(
    IPasswordHasher<User> passwordHasher,
    IUserRepository userRepository,
    IManglerService manglerService,
    IGlobalSettings globalSettings,
    ILicensingService? licenseService) : IScene<SingleUserScene.Request, SingleUserSceneResult>
{
    public class Request
    {
        [Required]
        public required string Email { get; set; }
        [Required]
        public required string Password { get; set; }
        public bool EmailVerified { get; set; } = false;
        public bool Premium { get; set; } = false;
    }

    public async Task<SceneResult<SingleUserSceneResult>> SeedAsync(Request request)
    {
        var (user, keys) = UserSeeder.Create(
            request.Email,
            passwordHasher,
            manglerService,
            emailVerified: request.EmailVerified || request.Premium,
            premium: request.Premium,
            maxStorageGb: request.Premium ? (short)1 : null,
            password: request.Password);

        if (request.Premium)
        {
            user.PremiumExpirationDate = DateTime.UtcNow.AddYears(1);
        }

        await userRepository.CreateAsync(user);

        // Best-effort license write. Self-hosted instances hold only the public licensing
        // certificate, so token signing throws there (by design — see LicensingService.SignLicense).
        // Don't let that failure abort the seed; the user is already persisted.
        if (request.Premium && licenseService is not null && CoreHelpers.SettingHasValue(globalSettings.LicenseDirectory))
        {
            try
            {
                await WriteLicenseAsync(user, licenseService);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or CryptographicException)
            {
                Console.WriteLine($"[SingleUserScene] Non-fatal license write failure for user '{user.Id}': {ex}");
            }
        }

        return new SceneResult<SingleUserSceneResult>(
            result: new SingleUserSceneResult
            {
                UserId = user.Id,
                Kdf = user.Kdf.ToString(),
                KdfIterations = user.KdfIterations,
                Key = user.Key!,
                PublicKey = user.PublicKey!,
                PrivateKey = user.PrivateKey!,
                ApiKey = user.ApiKey!,
                DecryptedKeyB64 = keys.Key
            },
            mangleMap: manglerService.GetMangleMap());
    }

    private async Task WriteLicenseAsync(User user, ILicensingService ls)
    {
        var token = await ls.CreateUserTokenAsync(user, null!);
        if (string.IsNullOrWhiteSpace(token)) return;

        var license = new UserLicense
        {
            LicenseType = LicenseType.User,
            Id = user.Id,
            Email = user.Email,
            Name = user.Name,
            Premium = user.Premium,
            MaxStorageGb = user.MaxStorageGb,
            Issued = DateTime.UtcNow,
            Expires = user.PremiumExpirationDate?.AddDays(7),
            Version = 1,
            Token = token,
        };

        await ls.WriteUserLicenseAsync(user, license);
    }
}
