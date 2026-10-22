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
using Bit.Core.Settings;
using Bit.RustSDK;
using Bit.Seeder.Extensions;
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
/// linked via a ProviderOrganization whose key wraps the organization key under the provider key. For Billable MSP
/// and Business Unit providers each linked organization is converted into a managed client organization with the same
/// entity changes as <c>ProviderBillingService.AddExistingOrganization</c>, and each plan's allocated seats are the
/// summed seats of its linked clients; Pending and Reseller links leave the organization unchanged. Type/status pairs
/// production cannot create (Reseller not Created, MSP/Business Unit Created) are rejected, as are Stripe IDs on a
/// Pending provider, organizations already linked to a provider, and any request on a self-hosted instance. The
/// scene makes no Stripe calls: it does not cancel client subscriptions or move customer balances, and it only
/// links caller-supplied Stripe customer/subscription IDs to the provider.
/// </summary>
public class SingleProviderScene(
    IUserRepository userRepository,
    IOrganizationRepository organizationRepository,
    IProviderRepository providerRepository,
    IProviderUserRepository providerUserRepository,
    IProviderOrganizationRepository providerOrganizationRepository,
    IProviderPlanRepository providerPlanRepository,
    IGlobalSettings globalSettings,
    IManglerService manglerService) : IScene<SingleProviderScene.Request, SingleProviderSceneResult>
{
    public class Request
    {
        [Required]
        public required Guid OwnerUserId { get; set; }
        [Required]
        public required string Name { get; set; }
        public ProviderType Type { get; set; } = ProviderType.Msp;
        public ProviderStatusType? Status { get; set; }
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
        globalSettings.ThrowIfSelfHosted("Providers");

        var status = ResolveStatus(request);
        var convertsToManagedClient =
            request.Type.SupportsConsolidatedBilling() && status == ProviderStatusType.Billable;

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

        await providerOrganizationRepository.ThrowIfOrganizationsLinkedToProviderAsync(
            request.Organizations.Select(o => o.OrganizationId));

        var providerKey = RustSdkService.GenerateOrganizationKeys().Key;

        var clients = new List<(Organization Organization, string WrappedKey, PlanType? ManagedPlanType)>();
        foreach (var link in request.Organizations)
        {
            var organization = await organizationRepository.GetByIdAsync(link.OrganizationId)
                ?? throw new InvalidOperationException($"Organization {link.OrganizationId} not found.");
            var managedPlanType = convertsToManagedClient
                ? GetManagedPlanType(request, organization)
                : (PlanType?)null;
            // Must be WrapSymmetricKey (raw key bytes), not EncryptString (base64 text), so clients unwrap it
            // back into the organization key.
            var wrappedKey = RustSdkService.WrapSymmetricKey(link.OrganizationKeyB64, providerKey);
            clients.Add((organization, wrappedKey, managedPlanType));
        }

        var domain = string.IsNullOrWhiteSpace(request.Domain)
            ? $"{Guid.NewGuid():N}.provider.test"
            : request.Domain;

        var provider = ProviderSeeder.Create(
            new ProviderSeed
            {
                Name = request.Name,
                Domain = domain,
                Type = request.Type,
                Status = status,
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
        foreach (var (organization, wrappedKey, managedPlanType) in clients)
        {
            if (managedPlanType is { } planType)
            {
                ConvertToManagedClient(organization, provider, planType);
                await organizationRepository.ReplaceAsync(organization);
            }

            var providerOrganization = new ProviderOrganization
            {
                ProviderId = provider.Id,
                OrganizationId = organization.Id,
                Key = wrappedKey
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

    private static ProviderStatusType ResolveStatus(Request request)
    {
        var status = request.Status
            ?? (request.Type == ProviderType.Reseller ? ProviderStatusType.Created : ProviderStatusType.Billable);

        var producible = request.Type == ProviderType.Reseller
            ? status == ProviderStatusType.Created
            : status != ProviderStatusType.Created;
        if (!producible)
        {
            var allowed = request.Type == ProviderType.Reseller ? "Created" : "Pending or Billable";
            throw new InvalidOperationException(
                $"A {request.Type} provider cannot have status {status}; status must be {allowed}.");
        }

        if (status == ProviderStatusType.Pending &&
            (!string.IsNullOrEmpty(request.GatewayCustomerId) || !string.IsNullOrEmpty(request.GatewaySubscriptionId)))
        {
            throw new InvalidOperationException(
                "A Pending provider has not completed setup and cannot have Stripe gateway IDs; " +
                "omit gatewayCustomerId and gatewaySubscriptionId, or use status Billable.");
        }

        return status;
    }

    private static PlanType GetManagedPlanType(Request request, Organization organization)
    {
        if (request.Type == ProviderType.BusinessUnit)
        {
            var businessUnitPlanType = request.Plans.FirstOrDefault()?.PlanType;
            if (businessUnitPlanType is not (PlanType.EnterpriseMonthly or PlanType.EnterpriseAnnually))
            {
                throw new InvalidOperationException(
                    "A Business Unit provider manages clients on its first plan, which must be EnterpriseMonthly or " +
                    $"EnterpriseAnnually; plans[0] is {businessUnitPlanType?.ToString() ?? "missing"}.");
            }

            return businessUnitPlanType.Value;
        }

        PlanType? managedPlanType = organization.PlanType switch
        {
            var planType when PlanConstants.TeamsPlanTypes.Contains(planType) => PlanType.TeamsMonthly,
            var planType when PlanConstants.EnterprisePlanTypes.Contains(planType) => PlanType.EnterpriseMonthly,
            _ => null
        };

        if (managedPlanType is not { } resolved)
        {
            throw new InvalidOperationException(
                $"Organization {organization.Id} is on {organization.PlanType}; " +
                "an MSP provider can only manage Teams or Enterprise organizations.");
        }

        if (request.Plans.All(p => p.PlanType != resolved))
        {
            throw new InvalidOperationException(
                $"Organization {organization.Id} on {organization.PlanType} is managed on {resolved}; " +
                $"add {resolved} to plans.");
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

        organization.BillingEmail = provider.BillingEmail!;
        organization.GatewaySubscriptionId = null;
        organization.ExpirationDate = null;
        organization.MaxAutoscaleSeats = null;
        organization.Status = OrganizationStatusType.Managed;
    }
}
