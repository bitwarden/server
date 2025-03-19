﻿#nullable enable
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Pricing;
using Bit.Core.Billing.Services.Contracts;
using Bit.Core.Entities;

namespace Bit.Core.Billing.Services.Implementations.AutomaticTax;

public class AutomaticTaxFactory(IPricingClient pricingClient) : IAutomaticTaxFactory
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
            return new PersonalUseAutomaticTaxStrategy();
        }

        if (parameters.PlanType.HasValue)
        {
            var plan = await pricingClient.GetPlanOrThrow(parameters.PlanType.Value);
            return plan.CanBeUsedByBusiness
                ? new BusinessUseAutomaticTaxStrategy()
                : new PersonalUseAutomaticTaxStrategy();
        }

        var personalUsePlans = await _personalUsePlansTask.Value;
        var plans = await pricingClient.ListPlans();

        if (personalUsePlans.Any(x => plans.Any(y => y.PasswordManager.StripePlanId == x)))
        {
            return new PersonalUseAutomaticTaxStrategy();
        }

        return new BusinessUseAutomaticTaxStrategy();
    }
}
