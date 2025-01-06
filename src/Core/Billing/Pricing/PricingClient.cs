using Bit.Core.Billing.Enums;
using Bit.Core.Models.StaticStore;
using Bit.Core.Services;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Proto.Billing.Pricing;

#nullable enable

namespace Bit.Core.Billing.Pricing;

public class PricingClient(
    IFeatureService featureService,
    GlobalSettings globalSettings) : IPricingClient
{
    public async Task<Plan?> GetPlan(PlanType planType)
    {
        var usePricingService = featureService.IsEnabled(FeatureFlagKeys.UsePricingService);

        if (!usePricingService)
        {
            return StaticStore.GetPlan(planType);
        }

        using var channel = GrpcChannel.ForAddress(globalSettings.PricingUri);
        var client = new PasswordManager.PasswordManagerClient(channel);

        var lookupKey = ToLookupKey(planType);
        if (string.IsNullOrEmpty(lookupKey))
        {
            return null;
        }

        try
        {
            var response =
                await client.GetPlanByLookupKeyAsync(new GetPlanByLookupKeyRequest { LookupKey = lookupKey });

            return new PlanAdapter(response);
        }
        catch (RpcException rpcException) when (rpcException.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<List<Plan>> ListPlans()
    {
        var usePricingService = featureService.IsEnabled(FeatureFlagKeys.UsePricingService);

        if (!usePricingService)
        {
            return StaticStore.Plans.ToList();
        }

        using var channel = GrpcChannel.ForAddress(globalSettings.PricingUri);
        var client = new PasswordManager.PasswordManagerClient(channel);

        var response = await client.ListPlansAsync(new Empty());
        return response.Plans.Select(Plan (plan) => new PlanAdapter(plan)).ToList();
    }

    private static string? ToLookupKey(PlanType planType)
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
