#nullable enable
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Extensions;
using Bit.Core.Billing.Pricing;
using Stripe;

namespace Bit.Core.Billing.Services.Implementations.AutomaticTax;

public class OrganizationAutomaticTaxStrategy(
    IPricingClient pricingClient) : IOrganizationAutomaticTaxStrategy
{
    private readonly Lazy<Task<IEnumerable<string>>> _familyPriceIdsTask = new(async () =>
    {
        var plans = await Task.WhenAll(
            pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually2019),
            pricingClient.GetPlanOrThrow(PlanType.FamiliesAnnually));

        return plans.Select(plan => plan.PasswordManager.StripePlanId);
    });

    public async Task<SubscriptionUpdateOptions?> GetUpdateOptionsAsync(Subscription subscription)
    {
        var shouldBeEnabled = await ShouldBeEnabledAsync(subscription);

        var options = new SubscriptionUpdateOptions
        {
            AutomaticTax = new SubscriptionAutomaticTaxOptions
            {
                Enabled = shouldBeEnabled
            },
            DefaultTaxRates = []
        };

        return options;
    }

    public async Task SetCreateOptionsAsync(SubscriptionCreateOptions options, Customer customer)
    {
        options.AutomaticTax = new SubscriptionAutomaticTaxOptions
        {
            Enabled = await ShouldBeEnabledAsync(options, customer)
        };
    }

    public async Task SetUpdateOptionsAsync(SubscriptionUpdateOptions options, Subscription subscription)
    {
        var shouldBeEnabled = await ShouldBeEnabledAsync(subscription);

        if (subscription.AutomaticTax.Enabled == shouldBeEnabled)
        {
            return;
        }

        options.AutomaticTax = new SubscriptionAutomaticTaxOptions
        {
            Enabled = shouldBeEnabled
        };
        options.DefaultTaxRates = [];
    }

    private async Task<bool> ShouldBeEnabledAsync(Subscription subscription)
    {
        if (!subscription.Customer.HasTaxLocationVerified())
        {
            return false;
        }

        bool shouldBeEnabled;
        if (subscription.Customer.Address.Country == "US")
        {
            shouldBeEnabled = true;
        }
        else
        {
            var familyPriceIds = await _familyPriceIdsTask.Value;
            shouldBeEnabled = subscription.Items.Select(item => item.Price.Id).Intersect(familyPriceIds).Any();
        }

        return shouldBeEnabled;
    }

    private async Task<bool> ShouldBeEnabledAsync(SubscriptionCreateOptions options, Customer customer)
    {
        if (!customer.HasTaxLocationVerified())
        {
            return false;
        }

        if (customer.Address.Country == "US")
        {
            return true;
        }

        var familyPriceIds = await _familyPriceIdsTask.Value;
        return options.Items.Select(item => item.Price).Intersect(familyPriceIds).Any();
    }
}
