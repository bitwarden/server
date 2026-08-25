using System.ComponentModel.DataAnnotations;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Utilities;
using Bit.RustSDK;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Options;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public struct SingleOrganizationSceneResult
{
    public Guid OrganizationId { get; init; }
    public Guid OrganizationUserId { get; init; }
    public string ApiKey { get; init; }
    public string OrganizationKeyB64 { get; init; }
}

/// <summary>
/// Seeds an organization on the requested plan and links an existing user to it as a confirmed owner.
/// </summary>
public class SingleOrganizationScene(
    IUserRepository userRepository,
    IOrganizationRepository organizationRepository,
    IOrganizationUserRepository organizationUserRepository,
    IOrganizationApiKeyRepository organizationApiKeyRepository,
    IManglerService manglerService) : IScene<SingleOrganizationScene.Request, SingleOrganizationSceneResult>
{
    public class Request
    {
        [Required]
        public required Guid OwnerUserId { get; set; }
        [Required]
        public required PlanType PlanType { get; set; }
        [Required]
        public required string Name { get; set; }
        [Required]
        public required string Domain { get; set; }
        [Required]
        public required int Seats { get; set; }
        public bool EnableSecretsManager { get; set; }
        public int? SmSeats { get; set; }
        public int? SmServiceAccounts { get; set; }
        public OrganizationOverrides? Overrides { get; set; }
        public GatewayType? Gateway { get; set; }
        public string? GatewayCustomerId { get; set; }
        public string? GatewaySubscriptionId { get; set; }
    }

    public async Task<SceneResult<SingleOrganizationSceneResult>> SeedAsync(Request request)
    {
        var user = await userRepository.GetByIdAsync(request.OwnerUserId);
        if (user == null)
        {
            throw new InvalidOperationException($"User with ID {request.OwnerUserId} not found.");
        }

        if (string.IsNullOrEmpty(user.PublicKey))
        {
            throw new InvalidOperationException(
                $"User {request.OwnerUserId} has no public key; cannot encrypt the organization key for the owner.");
        }

        var orgKeys = RustSdkService.GenerateOrganizationKeys();

        var organization = OrganizationSeeder.Create(
            new OrganizationSeed
            {
                Name = request.Name,
                Domain = request.Domain,
                Seats = request.Seats,
                PlanType = request.PlanType,
                PublicKey = orgKeys.PublicKey,
                PrivateKey = orgKeys.PrivateKey,
                Overrides = request.Overrides,
                Gateway = request.Gateway,
                GatewayCustomerId = request.GatewayCustomerId,
                GatewaySubscriptionId = request.GatewaySubscriptionId,
                EnableSecretsManager = request.EnableSecretsManager,
                SmSeats = request.SmSeats,
                SmServiceAccounts = request.SmServiceAccounts
            },
            manglerService);

        await organizationRepository.CreateAsync(organization);

        var ownerOrgKey = RustSdkService.GenerateUserOrganizationKey(user.PublicKey, orgKeys.Key);
        var organizationUser = organization.CreateOrganizationUserWithKey(
            user,
            OrganizationUserType.Owner,
            OrganizationUserStatusType.Confirmed,
            ownerOrgKey);

        organizationUser.AccessSecretsManager = organization.UseSecretsManager;

        await organizationUserRepository.CreateAsync(organizationUser);

        var apiKey = new OrganizationApiKey
        {
            Id = CombGuid.Generate(),
            OrganizationId = organization.Id,
            Type = OrganizationApiKeyType.Default,
            ApiKey = CoreHelpers.SecureRandomString(30),
            RevisionDate = DateTime.UtcNow,
        };

        await organizationApiKeyRepository.CreateAsync(apiKey);

        return new SceneResult<SingleOrganizationSceneResult>(
            result: new SingleOrganizationSceneResult
            {
                OrganizationId = organization.Id,
                OrganizationUserId = organizationUser.Id,
                ApiKey = apiKey.ApiKey,
                OrganizationKeyB64 = orgKeys.Key
            },
            mangleMap: manglerService.GetMangleMap());
    }
}
