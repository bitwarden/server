using System.Net;
using System.Net.Http.Json;
using Bit.Core.Billing.Constants;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing.Organizations;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Settings;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Billing.Pricing;

using OrganizationPlan = Bit.Core.Models.StaticStore.Plan;
using PremiumPlan = Premium.Plan;
using Purchasable = Premium.Purchasable;

public class PricingClient(
    IFeatureService featureService,
    GlobalSettings globalSettings,
    HttpClient httpClient,
    ILogger<PricingClient> logger) : IPricingClient
{
    public async Task<OrganizationPlan?> GetPlan(PlanType planType)
    {
        if (globalSettings.SelfHosted)
        {
            return null;
        }

        var lookupKey = GetLookupKey(planType);

        if (lookupKey == null)
        {
            logger.LogError("Could not find Pricing Service lookup key for PlanType {PlanType}", planType);
            return null;
        }

        var response = await httpClient.GetAsync($"plans/organization/{lookupKey}");

        if (response.IsSuccessStatusCode)
        {
            var plan = await response.Content.ReadFromJsonAsync<Plan>();
            return plan == null
                ? throw new BillingException(message: "Deserialization of Pricing Service response resulted in null")
                : new PlanAdapter(PreProcessFamiliesPreMigrationPlan(plan));
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogError("Pricing Service plan for PlanType {PlanType} was not found", planType);
            return null;
        }

        throw new BillingException(
            message: $"Request to the Pricing Service failed with status code {response.StatusCode}");
    }

    public async Task<OrganizationPlan> GetPlanOrThrow(PlanType planType)
    {
        var plan = await GetPlan(planType);

        return plan ?? throw new NotFoundException($"Could not find plan for type {planType}");
    }

    public async Task<List<OrganizationPlan>> ListPlans()
    {
        if (globalSettings.SelfHosted)
        {
            return [];
        }

        var response = await httpClient.GetAsync("plans/organization");

        if (response.IsSuccessStatusCode)
        {
            var plans = await response.Content.ReadFromJsonAsync<List<Plan>>();
            return plans == null
                ? throw new BillingException(message: "Deserialization of Pricing Service response resulted in null")
                : plans.Select(OrganizationPlan (plan) => new PlanAdapter(PreProcessFamiliesPreMigrationPlan(plan))).ToList();
        }

        throw new BillingException(
            message: $"Request to the Pricing Service failed with status {response.StatusCode}");
    }

    public async Task<PremiumPlan> GetAvailablePremiumPlan()
    {
        var premiumPlans = await ListPremiumPlans();

        var availablePlan = premiumPlans.FirstOrDefault(premiumPlan => premiumPlan.Available);

        return availablePlan ?? throw new NotFoundException("Could not find available premium plan");
    }

    public async Task<List<PremiumPlan>> ListPremiumPlans()
    {
        if (globalSettings.SelfHosted)
        {
            return [];
        }

        var fetchPremiumPriceFromPricingService =
            featureService.IsEnabled(FeatureFlagKeys.PM26793_FetchPremiumPriceFromPricingService);

        if (!fetchPremiumPriceFromPricingService)
        {
            return [CurrentPremiumPlan];
        }

        var response = await httpClient.GetAsync("plans/premium");

        if (response.IsSuccessStatusCode)
        {
            var plans = await response.Content.ReadFromJsonAsync<List<PremiumPlan>>();
            return plans ?? throw new BillingException(message: "Deserialization of Pricing Service response resulted in null");
        }

        throw new BillingException(
            message: $"Request to the Pricing Service failed with status {response.StatusCode}");
    }

    private string? GetLookupKey(PlanType planType)
        => planType switch
        {
            PlanType.EnterpriseAnnually => "enterprise-annually",
            PlanType.EnterpriseAnnually2019 => "enterprise-annually-2019",
            PlanType.EnterpriseAnnually2020 => "enterprise-annually-2020",
            PlanType.EnterpriseAnnually2023 => "enterprise-annually-2023",
            PlanType.EnterpriseMonthly => "enterprise-monthly",
            PlanType.EnterpriseMonthly2019 => "enterprise-monthly-2019",
            PlanType.EnterpriseMonthly2020 => "enterprise-monthly-2020",
            PlanType.EnterpriseMonthly2023 => "enterprise-monthly-2023",
            PlanType.FamiliesAnnually => "families",
            PlanType.FamiliesAnnually2025 =>
                featureService.IsEnabled(FeatureFlagKeys.PM26462_Milestone_3)
                    ? "families-2025"
                    : "families",
            PlanType.FamiliesAnnually2019 => "families-2019",
            PlanType.Free => "free",
            PlanType.TeamsAnnually => "teams-annually",
            PlanType.TeamsAnnually2019 => "teams-annually-2019",
            PlanType.TeamsAnnually2020 => "teams-annually-2020",
            PlanType.TeamsAnnually2023 => "teams-annually-2023",
            PlanType.TeamsMonthly => "teams-monthly",
            PlanType.TeamsMonthly2019 => "teams-monthly-2019",
            PlanType.TeamsMonthly2020 => "teams-monthly-2020",
            PlanType.TeamsMonthly2023 => "teams-monthly-2023",
            PlanType.TeamsStarter => "teams-starter",
            PlanType.TeamsStarter2023 => "teams-starter-2023",
            _ => null
        };

    /// <summary>
    /// Safeguard used until the feature flag is enabled. Pricing service will return the
    /// 2025PreMigration plan with "families" lookup key. When that is detected and the FF
    /// is still disabled, set the lookup key to families-2025 so PlanAdapter will assign
    /// the correct plan.
    /// </summary>
    /// <param name="plan">The plan to preprocess</param>
    private Plan PreProcessFamiliesPreMigrationPlan(Plan plan)
    {
        if (plan.LookupKey == "families" && !featureService.IsEnabled(FeatureFlagKeys.PM26462_Milestone_3))
            plan.LookupKey = "families-2025";
        return plan;
    }

    private static PremiumPlan CurrentPremiumPlan => new()
    {
        Name = "Premium",
        Available = true,
        LegacyYear = null,
        Seat = new Purchasable { Price = 10M, StripePriceId = StripeConstants.Prices.PremiumAnnually },
        Storage = new Purchasable { Price = 4M, StripePriceId = StripeConstants.Prices.StoragePlanPersonal, Provided = 1 }
    };
}
