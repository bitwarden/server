using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Repositories;
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
    ILicensingService licenseService) : IScene<SingleUserScene.Request, SingleUserSceneResult>
{
    public class Request
    {
        [Required]
        public required string Email { get; set; }
        [Required]
        public required string Password { get; set; }
        public bool EmailVerified { get; set; } = false;
        public bool Premium { get; set; } = false;
        public bool SelfHosted { get; set; } = false;
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

        if (request.SelfHosted && user.Premium)
        {
            try
            {
                await SelfHostLicenseService.WriteLicenseAsync(licenseService, user);
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

}
