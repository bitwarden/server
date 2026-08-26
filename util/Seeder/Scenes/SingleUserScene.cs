using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

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
    public bool PremiumLicenseWritten { get; init; }
    public string? PremiumLicenseWarning { get; init; }
}

/// <summary>
/// Creates a single user using the provided account details.
/// </summary>
public class SingleUserScene(
    IPasswordHasher<User> passwordHasher,
    IUserRepository userRepository,
    IManglerService manglerService,
    ILicensingService licenseService,
    ISeederLicenseSigner licenseSigner,
    ILogger<SingleUserScene> logger) : IScene<SingleUserScene.Request, SingleUserSceneResult>
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
        public GatewayType? Gateway { get; set; }
        public string? GatewayCustomerId { get; set; }
        public string? GatewaySubscriptionId { get; set; }
    }

    public async Task<SceneResult<SingleUserSceneResult>> SeedAsync(Request request)
    {
        var (user, keys) = UserSeeder.Create(
            new UserSeed
            {
                Email = request.Email,
                EmailVerified = request.EmailVerified || request.Premium,
                Premium = request.Premium,
                MaxStorageGb = request.Premium ? (short)1 : null,
                Password = request.Password,
                Gateway = request.Gateway,
                GatewayCustomerId = request.GatewayCustomerId,
                GatewaySubscriptionId = request.GatewaySubscriptionId
            },
            passwordHasher,
            manglerService);

        await userRepository.CreateAsync(user);

        var licenseOutcome = default(LicenseWriteOutcome);
        if (request.SelfHosted && user.Premium)
        {
            licenseOutcome = await SelfHostLicenseService.WriteLicenseAsync(licenseService, licenseSigner, user, logger);
        }

        return new SceneResult<SingleUserSceneResult>(
            result: new SingleUserSceneResult
            {
                PremiumLicenseWritten = licenseOutcome.Written,
                PremiumLicenseWarning = licenseOutcome.Warning,
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
