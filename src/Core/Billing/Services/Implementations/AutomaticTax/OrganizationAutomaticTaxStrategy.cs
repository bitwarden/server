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

    public async Task<SubscriptionUpdateOptions> GetUpdateOptionsAsync(Subscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        var isEnabled = await IsEnabledAsync(subscription);
        if (!isEnabled.HasValue)
        {
            return null;
        }

        var options = new SubscriptionUpdateOptions
        {
            AutomaticTax = new SubscriptionAutomaticTaxOptions
            {
                Enabled = isEnabled.Value
            },
            DefaultTaxRates = []
        };

        return options;
    }

    public async Task SetCreateOptionsAsync(SubscriptionCreateOptions options, Customer customer)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(customer);

        options.AutomaticTax = new SubscriptionAutomaticTaxOptions
        {
            Enabled = await IsEnabledAsync(options, customer)
        };
    }

    public async Task SetUpdateOptionsAsync(SubscriptionUpdateOptions options, Subscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        if (subscription.AutomaticTax.Enabled == options.AutomaticTax?.Enabled)
        {
            return;
        }

        var isEnabled = await IsEnabledAsync(subscription);
        if (!isEnabled.HasValue)
        {
            return;
        }

        options.AutomaticTax = new SubscriptionAutomaticTaxOptions
        {
            Enabled = isEnabled.Value
        };
        options.DefaultTaxRates = [];
    }

    private async Task<bool?> IsEnabledAsync(Subscription subscription)
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

        if (subscription.AutomaticTax.Enabled != shouldBeEnabled)
        {
            return shouldBeEnabled;
        }

        return null;
    }

    private async Task<bool?> IsEnabledAsync(SubscriptionCreateOptions options, Customer customer)
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
