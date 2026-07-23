using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Providers.Repositories;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.RustSDK;
using Bit.Seeder.Factories;
using Bit.Seeder.Services;

namespace Bit.Seeder.Scenes;

public struct SingleProviderSceneResult
{
    public Guid ProviderId { get; init; }
    public ProviderType ProviderType { get; init; }
    public Guid ProviderUserId { get; init; }
    public string ProviderKeyB64 { get; init; }
    public Guid[] ProviderOrganizationIds { get; init; }
}

/// <summary>
/// Links an existing owner user and existing client organizations to a new provider. The owner is added as
/// a confirmed ProviderAdmin, the requested billing plans are configured, and each supplied organization is
/// linked via a ProviderOrganization whose key wraps the organization key under the provider key. Existing
/// Stripe customer/subscription IDs can be linked; the scene does not call the Stripe API — it only links
/// caller-supplied identifiers.
/// </summary>
public class SingleProviderScene(
    IUserRepository userRepository,
    IProviderRepository providerRepository,
    IProviderUserRepository providerUserRepository,
    IProviderOrganizationRepository providerOrganizationRepository,
    IProviderPlanRepository providerPlanRepository,
    IManglerService manglerService) : IScene<SingleProviderScene.Request, SingleProviderSceneResult>
{
    public class Request
    {
        [Required]
        public required Guid OwnerUserId { get; set; }
        [Required]
        public required string Name { get; set; }
        public ProviderType Type { get; set; } = ProviderType.Msp;
        public List<PlanConfig> Plans { get; set; } = [];
        public List<OrganizationLink> Organizations { get; set; } = [];
        public string? Domain { get; set; }
        public string? GatewayCustomerId { get; set; }
        public string? GatewaySubscriptionId { get; set; }
    }

    public class PlanConfig
    {
        [Required]
        public required PlanType PlanType { get; set; }
        public int Seats { get; set; }
    }

    public class OrganizationLink
    {
        [Required]
        public required Guid OrganizationId { get; set; }
        [Required]
        public required string OrganizationKeyB64 { get; set; }
    }

    public async Task<SceneResult<SingleProviderSceneResult>> SeedAsync(Request request)
    {
        var owner = await userRepository.GetByIdAsync(request.OwnerUserId);
        if (owner == null)
        {
            throw new InvalidOperationException($"User with ID {request.OwnerUserId} not found.");
        }

        if (string.IsNullOrEmpty(owner.PublicKey))
        {
            throw new InvalidOperationException(
                $"User {request.OwnerUserId} has no public key; cannot encrypt the provider key for the owner.");
        }

        var domain = string.IsNullOrWhiteSpace(request.Domain)
            ? $"{Guid.NewGuid():N}.provider.test"
            : request.Domain;
        var providerKey = RustSdkService.GenerateOrganizationKeys().Key;

        var provider = ProviderSeeder.Create(request.Name, domain, request.Type, manglerService);
        ProviderSeeder.ApplyBilling(
            provider,
            GatewayType.Stripe,
            request.GatewayCustomerId,
            request.GatewaySubscriptionId);
        await providerRepository.CreateAsync(provider);

        var providerUser = ProviderUserSeeder.CreateConfirmedAdmin(provider, owner, providerKey);
        await providerUserRepository.CreateAsync(providerUser);

        foreach (var plan in request.Plans)
        {
            var providerPlan = ProviderPlanSeeder.Create(provider, plan.PlanType, plan.Seats);
            await providerPlanRepository.CreateAsync(providerPlan);
        }

        var providerOrganizationIds = new List<Guid>();
        foreach (var link in request.Organizations)
        {
            var providerOrganization = new ProviderOrganization
            {
                ProviderId = provider.Id,
                OrganizationId = link.OrganizationId,
                // Wrap the organization key under the provider key so clients unwrap it back into the
                // organization key. Must be WrapSymmetricKey (raw key bytes), not EncryptString (base64 text).
                Key = RustSdkService.WrapSymmetricKey(link.OrganizationKeyB64, providerKey)
            };
            await providerOrganizationRepository.CreateAsync(providerOrganization);
            providerOrganizationIds.Add(providerOrganization.Id);
        }

        return new SceneResult<SingleProviderSceneResult>(
            result: new SingleProviderSceneResult
            {
                ProviderId = provider.Id,
                ProviderType = provider.Type,
                ProviderUserId = providerUser.Id,
                ProviderKeyB64 = providerKey,
                ProviderOrganizationIds = providerOrganizationIds.ToArray()
            },
            mangleMap: manglerService.GetMangleMap());
    }
}
