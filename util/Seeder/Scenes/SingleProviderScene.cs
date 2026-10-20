using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Providers.Repositories;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.RustSDK;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
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
/// linked via a ProviderOrganization whose key wraps the organization key under the provider key. For MSP and
/// Business Unit providers each linked organization is converted into a managed client organization, mirroring
/// <c>ProviderBillingService.AddExistingOrganization</c>, and each plan's allocated seats are the summed seats of
/// its linked clients; Reseller links leave the organization unchanged. Existing Stripe customer/subscription IDs
/// can be linked to the provider; the scene does not call the Stripe API, it only links caller-supplied identifiers.
/// </summary>
public class SingleProviderScene(
    IUserRepository userRepository,
    IOrganizationRepository organizationRepository,
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

        /// <summary>
        /// The plan's seat minimum. Allocated seats are derived from the linked client organizations.
        /// </summary>
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

        var clients = new List<(Organization Organization, OrganizationLink Link, PlanType? ManagedPlanType)>();
        foreach (var link in request.Organizations)
        {
            var organization = await organizationRepository.GetByIdAsync(link.OrganizationId)
                ?? throw new InvalidOperationException($"Organization {link.OrganizationId} not found.");
            var managedPlanType = request.Type.SupportsConsolidatedBilling()
                ? GetManagedPlanType(request, organization)
                : (PlanType?)null;
            clients.Add((organization, link, managedPlanType));
        }

        var domain = string.IsNullOrWhiteSpace(request.Domain)
            ? $"{Guid.NewGuid():N}.provider.test"
            : request.Domain;
        var providerKey = RustSdkService.GenerateOrganizationKeys().Key;

        var provider = ProviderSeeder.Create(
            new ProviderSeed
            {
                Name = request.Name,
                Domain = domain,
                Type = request.Type,
                GatewayCustomerId = request.GatewayCustomerId,
                GatewaySubscriptionId = request.GatewaySubscriptionId
            },
            manglerService);
        await providerRepository.CreateAsync(provider);

        var providerUser = ProviderUserSeeder.CreateConfirmedAdmin(provider, owner, providerKey);
        await providerUserRepository.CreateAsync(providerUser);

        foreach (var plan in request.Plans)
        {
            var allocatedSeats = clients
                .Where(c => c.ManagedPlanType == plan.PlanType)
                .Sum(c => c.Organization.Seats ?? 0);
            var providerPlan = ProviderPlanSeeder.Create(provider, plan.PlanType, plan.Seats, allocatedSeats);
            await providerPlanRepository.CreateAsync(providerPlan);
        }

        var providerOrganizationIds = new List<Guid>();
        foreach (var (organization, link, managedPlanType) in clients)
        {
            if (managedPlanType is { } planType)
            {
                ConvertToManagedClient(organization, provider, planType);
                await organizationRepository.ReplaceAsync(organization);
            }

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

    private static PlanType GetManagedPlanType(Request request, Organization organization)
    {
        var businessUnitPlanType = request.Plans.FirstOrDefault()?.PlanType;
        var managedPlanType = request.Type == ProviderType.BusinessUnit
            ? businessUnitPlanType is PlanType.EnterpriseMonthly or PlanType.EnterpriseAnnually
                ? businessUnitPlanType
                : null
            : organization.PlanType switch
            {
                var planType when PlanConstants.TeamsPlanTypes.Contains(planType) => PlanType.TeamsMonthly,
                var planType when PlanConstants.EnterprisePlanTypes.Contains(planType) => PlanType.EnterpriseMonthly,
                _ => (PlanType?)null
            };

        if (managedPlanType is not { } resolved || request.Plans.All(p => p.PlanType != resolved))
        {
            throw new InvalidOperationException(
                $"Organization {organization.Id} on {organization.PlanType} has no matching {request.Type} provider plan.");
        }

        return resolved;
    }

    private static void ConvertToManagedClient(Organization organization, Provider provider, PlanType managedPlanType)
    {
        var plan = new Organization();
        PlanFeatures.Apply(plan, managedPlanType);

        organization.Plan = plan.Plan;
        organization.PlanType = plan.PlanType;
        organization.MaxCollections = plan.MaxCollections;
        organization.MaxStorageGb = plan.MaxStorageGb;
        organization.UsePolicies = plan.UsePolicies;
        organization.UseMyItems = plan.UseMyItems;
        organization.UseSso = plan.UseSso;
        organization.UseOrganizationDomains = plan.UseOrganizationDomains;
        organization.UseGroups = plan.UseGroups;
        organization.UseEvents = plan.UseEvents;
        organization.UseDirectory = plan.UseDirectory;
        organization.UseTotp = plan.UseTotp;
        organization.Use2fa = plan.Use2fa;
        organization.UseApi = plan.UseApi;
        organization.UseResetPassword = plan.UseResetPassword;
        organization.SelfHost = plan.SelfHost;
        organization.UsersGetPremium = plan.UsersGetPremium;
        organization.UseCustomPermissions = plan.UseCustomPermissions;
        organization.UseScim = plan.UseScim;
        organization.UseRiskInsights = plan.UseRiskInsights;
        organization.UseKeyConnector = plan.UseKeyConnector;

        organization.UseSecretsManager = false;
        organization.BillingEmail = provider.BillingEmail!;
        organization.GatewayCustomerId = null;
        organization.GatewaySubscriptionId = null;
        organization.ExpirationDate = null;
        organization.MaxAutoscaleSeats = null;
        organization.Status = OrganizationStatusType.Managed;
    }
}
