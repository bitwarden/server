using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Models.StaticStore;
using Bit.RustSDK;
using Bit.Seeder.Factories;
using Bit.Seeder.Models;
using Bit.Seeder.Options;
using Bit.Seeder.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bit.Seeder.Steps;

/// <summary>
/// Creates an organization from a fixture or explicit parameters and applies plan-derived
/// baselines (Seats, MaxStorageGb, SmSeats, SmServiceAccounts) so the seeded row mirrors
/// what <c>CloudOrganizationSignUpCommand</c> would produce.
/// </summary>
/// <remarks>
/// Async because <c>IPricingClient</c> is HTTP-based. If the pricing service is unreachable
/// the step logs a warning and falls back to the pre-pricing defaults set by
/// <see cref="PlanFeatures.Apply"/>; the org is still created.
/// </remarks>
internal sealed class CreateOrganizationStep : IAsyncStep
{
    private readonly string? _fixtureName;
    private readonly string? _name;
    private readonly string? _domain;
    private readonly int? _seats;
    private readonly PlanType _planType;
    private readonly OrganizationOverrides? _overrides;

    private CreateOrganizationStep(
        string? fixtureName,
        string? name,
        string? domain,
        int? seats,
        PlanType planType,
        OrganizationOverrides? overrides)
    {
        if (fixtureName is null && (name is null || domain is null))
        {
            throw new ArgumentException(
                "Either fixtureName OR (name AND domain) must be provided.");
        }

        _fixtureName = fixtureName;
        _name = name;
        _domain = domain;
        _seats = seats;
        _planType = planType;
        _overrides = overrides;
    }

    internal static CreateOrganizationStep FromFixture(
        string fixtureName,
        string? planType = null,
        int? seats = null,
        OrganizationOverrides? overrides = null) =>
        new(fixtureName, null, null, seats, PlanFeatures.Parse(planType), overrides);

    internal static CreateOrganizationStep FromParams(
        string name,
        string domain,
        int? seats = null,
        PlanType planType = PlanType.EnterpriseAnnually,
        OrganizationOverrides? overrides = null) =>
        new(null, name, domain, seats, planType, overrides);

    public async Task ExecuteAsync(SeederContext context)
    {
        string name, domain;

        if (_fixtureName is not null)
        {
            var fixture = context.GetSeedReader().Read<SeedOrganization>($"organizations.{_fixtureName}");
            name = fixture.Name;
            domain = fixture.Domain;
        }
        else
        {
            name = _name!;
            domain = _domain!;
        }

        var seats = _seats ?? PlanFeatures.GenerateRealisticSeatCount(_planType, domain);
        var orgKeys = RustSdkService.GenerateOrganizationKeys();
        var organization = OrganizationSeeder.Create(name, domain, seats, context.GetMangler(), orgKeys.PublicKey, orgKeys.PrivateKey, _planType);

        PlanFeatures.ApplyOrganizationOverrides(organization, _overrides);

        var plan = await TryGetPlanAsync(context, _planType);
        if (plan is not null)
        {
            ApplyPlanBaselines(organization, plan);
            context.Plan = plan;
        }

        context.Organization = organization;
        context.OrgKeys = orgKeys;
        context.Domain = domain;

        context.Organizations.Add(organization);
    }

    /// <summary>
    /// Applies plan-derived capacity values, matching <c>CloudOrganizationSignUpCommand.cs:78-115</c>:
    /// PM seats clamp up to the plan baseline; storage clamps up; SM seats and service accounts
    /// are stored as (baseline + existing-as-additional).
    /// </summary>
    private static void ApplyPlanBaselines(Core.AdminConsole.Entities.Organization organization, Plan plan)
    {
        organization.Seats = Math.Max(plan.PasswordManager.BaseSeats, organization.Seats ?? 0);

        if (plan.PasswordManager.BaseStorageGb > 0)
        {
            organization.MaxStorageGb = (short)Math.Max(plan.PasswordManager.BaseStorageGb, organization.MaxStorageGb ?? 0);
        }

        if (organization.UseSecretsManager)
        {
            organization.SmSeats = plan.SecretsManager.BaseSeats + (organization.SmSeats ?? 0);
            organization.SmServiceAccounts = plan.SecretsManager.BaseServiceAccount + (organization.SmServiceAccounts ?? 0);
        }
    }

    private static async Task<Plan?> TryGetPlanAsync(SeederContext context, PlanType planType)
    {
        var pricingClient = context.Services.GetService<IPricingClient>();
        if (pricingClient is null)
        {
            return null;
        }

        try
        {
            return await pricingClient.GetPlanOrThrow(planType);
        }
        catch (Exception ex)
        {
            var logger = context.Services
                .GetService<ILoggerFactory>()
                ?.CreateLogger(typeof(CreateOrganizationStep));
            logger?.LogWarning(ex,
                "Seeder: could not fetch plan {PlanType} from the pricing service. Falling back to PlanFeatures defaults; some capacity fields may not match production exactly.",
                planType);
            return null;
        }
    }
}
