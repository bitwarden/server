using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Services;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.KeyManagement.Kdf;
using Bit.Core.Repositories;
using Bit.Core.Settings;
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
    Func<ILicensingService> licenseServiceFactory,
    ISeederLicenseSigner licenseSigner,
    IGlobalSettings globalSettings,
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
        /// <summary>
        /// PBKDF2 iteration count. Null uses <see cref="UserSeed.DefaultKdfIterations"/>.
        /// </summary>
        public int? KdfIterations { get; set; }
    }

    private const int MinKdfIterations = 5_000;

    public async Task<SceneResult<SingleUserSceneResult>> SeedAsync(Request request)
    {
        if (request.KdfIterations is { } kdfIterations &&
            (kdfIterations < MinKdfIterations || kdfIterations > KdfConstants.PBKDF2_ITERATIONS.Max))
        {
            throw new ArgumentOutOfRangeException(nameof(request.KdfIterations), kdfIterations,
                $"KdfIterations must be between {MinKdfIterations:N0} and {KdfConstants.PBKDF2_ITERATIONS.Max:N0}.");
        }

        if (request.SelfHosted && request.Premium && !globalSettings.SelfHosted)
        {
            throw new InvalidOperationException(
                "SelfHosted premium was requested, but this Seeder API is running in cloud mode " +
                "('globalSettings:selfHosted' is false), so no self-hosted license can be written. " +
                "Target a self-hosted Seeder API or set SelfHosted=false.");
        }

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
                GatewaySubscriptionId = request.GatewaySubscriptionId,
                KdfIterations = request.KdfIterations ?? UserSeed.DefaultKdfIterations
            },
            passwordHasher,
            manglerService);

        await userRepository.CreateAsync(user);

        var licenseOutcome = default(LicenseWriteOutcome);
        if (request.SelfHosted && user.Premium)
        {
            licenseOutcome = await SelfHostLicenseService.WriteLicenseAsync(licenseServiceFactory, licenseSigner, user, logger);
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
