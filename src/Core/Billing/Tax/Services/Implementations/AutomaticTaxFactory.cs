#nullable enable
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Tax.Models;
using Bit.Core.Entities;
using Bit.Core.Services;

namespace Bit.Core.Billing.Tax.Services.Implementations;

public class AutomaticTaxFactory(
    IFeatureService featureService,
    IPricingClient pricingClient) : IAutomaticTaxFactory
{
    public const string BusinessUse = "business-use";
    public const string PersonalUse = "personal-use";

    private readonly Lazy<Task<IEnumerable<string>>> _personalUsePlansTask = new(async () =>
    {
        var plans = await Task.WhenAll(
            pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019),
            pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually));

        return plans.Select(plan => plan.PasswordManager.StripePlanId);
    });

    public async Task<IAutomaticTaxStrategy> CreateAsync(AutomaticTaxFactoryParameters parameters)
    {
        if (parameters.Subscriber is User)
        {
            return new PersonalUseAutomaticTaxStrategy(featureService);
        }

        if (parameters.PlanType.HasValue)
        {
            var plan = await pricingClient.GetPlanOrThrow(parameters.PlanType.Value);
            return plan.CanBeUsedByBusiness
                ? new BusinessUseAutomaticTaxStrategy(featureService)
                : new PersonalUseAutomaticTaxStrategy(featureService);
        }

        var personalUsePlans = await _personalUsePlansTask.Value;

        if (parameters.Prices != null && parameters.Prices.Any(x => personalUsePlans.Any(y => y == x)))
        {
            return new PersonalUseAutomaticTaxStrategy(featureService);
        }

        return new BusinessUseAutomaticTaxStrategy(featureService);
    }
}
