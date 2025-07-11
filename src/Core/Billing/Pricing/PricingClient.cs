using System.Net;
using System.Net.Http.Json;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing.Models;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Microsoft.Extensions.Logging;
using Plan = Bit.Core.Models.StaticStore.Plan;

#nullable enable

namespace Bit.Core.Billing.Pricing;

public class PricingClient(
    IFeatureService featureService,
    GlobalSettings globalSettings,
    HttpClient httpClient,
    ILogger<PricingClient> logger) : IPricingClient
{
    public async Task<Plan?> GetPlan(PlanType planType)
    {
        if (globalSettings.SelfHosted)
        {
            return null;
        }

        var usePricingService = featureService.IsEnabled(FeatureFlagKeys.UsePricingService);

        if (!usePricingService)
        {
            return StaticStore.GetPlan(planType);
        }

        var lookupKey = GetLookupKey(planType);

        if (lookupKey == null)
        {
            logger.LogError("Could not find Pricing Service lookup key for PlanType {PlanType}", planType);
            return null;
        }

        var response = await httpClient.GetAsync($"plans/lookup/{lookupKey}");

        if (response.IsSuccessStatusCode)
        {
            var plan = await response.Content.ReadFromJsonAsync<Models.Plan>();
            if (plan == null)
            {
                throw new BillingException(message: "Deserialization of Pricing Service response resulted in null");
            }
            return new PlanAdapter(plan);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogError("Pricing Service plan for PlanType {PlanType} was not found", planType);
            return null;
        }

        throw new BillingException(
            message: $"Request to the Pricing Service failed with status code {response.StatusCode}");
    }

    public async Task<Plan> GetPlanOrThrow(PlanType planType)
    {
        var plan = await GetPlan(planType);

        if (plan == null)
        {
            throw new NotFoundException();
        }

        return plan;
    }

    public async Task<List<Plan>> ListPlans()
    {
        if (globalSettings.SelfHosted)
        {
            return [];
        }

        var usePricingService = featureService.IsEnabled(FeatureFlagKeys.UsePricingService);

        if (!usePricingService)
        {
            return StaticStore.Plans.ToList();
        }

        var response = await httpClient.GetAsync("plans");

        if (response.IsSuccessStatusCode)
        {
            var plans = await response.Content.ReadFromJsonAsync<List<Models.Plan>>();
            if (plans == null)
            {
                throw new BillingException(message: "Deserialization of Pricing Service response resulted in null");
            }
            return plans.Select(Plan (plan) => new PlanAdapter(plan)).ToList();
        }

        throw new BillingException(
            message: $"Request to the Pricing Service failed with status {response.StatusCode}");
    }

    private static string? GetLookupKey(PlanType planType)
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
}
