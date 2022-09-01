using Bit.Core.Entities;
using Stripe;

namespace Bit.Core.Models.Business;

public class OrganizationSubscriptionOptionsBase : Stripe.SubscriptionCreateOptions
{
    public OrganizationSubscriptionOptionsBase(Organization org, StaticStore.Plan plan, TaxInfo taxInfo, int additionalSeats, int additionalStorageGb, bool premiumAccessAddon)
    {
        Items = new List<SubscriptionItemOptions>();
        Metadata = new Dictionary<string, string>
        {
            [org.GatewayIdField()] = org.Id.ToString()
        };

        if (plan.StripePlanId != null)
        {
            Items.Add(new SubscriptionItemOptions
            {
                Plan = plan.StripePlanId,
                Quantity = 1
            });
        }

        if (additionalSeats > 0 && plan.StripeSeatPlanId != null)
        {
            Items.Add(new SubscriptionItemOptions
            {
                Plan = plan.StripeSeatPlanId,
                Quantity = additionalSeats
            });
        }

        if (additionalStorageGb > 0)
        {
            Items.Add(new SubscriptionItemOptions
            {
                Plan = plan.StripeStoragePlanId,
                Quantity = additionalStorageGb
            });
        }

        if (premiumAccessAddon && plan.StripePremiumAccessPlanId != null)
        {
            Items.Add(new SubscriptionItemOptions
            {
                Plan = plan.StripePremiumAccessPlanId,
                Quantity = 1
            });
        }

        if (!string.IsNullOrWhiteSpace(taxInfo?.StripeTaxRateId))
        {
            DefaultTaxRates = new List<string> { taxInfo.StripeTaxRateId };
        }
    }
}

public class OrganizationPurchaseSubscriptionOptions : OrganizationSubscriptionOptionsBase
{
    public OrganizationPurchaseSubscriptionOptions(
        Organization org, StaticStore.Plan plan,
        TaxInfo taxInfo, int additionalSeats = 0,
        int additionalStorageGb = 0, bool premiumAccessAddon = false) :
        base(org, plan, taxInfo, additionalSeats, additionalStorageGb, premiumAccessAddon)
    {
        OffSession = true;
        TrialPeriodDays = plan.TrialPeriodDays;
    }
}

public class OrganizationUpgradeSubscriptionOptions : OrganizationSubscriptionOptionsBase
{
    public OrganizationUpgradeSubscriptionOptions(
        string customerId, Organization org,
        StaticStore.Plan plan, TaxInfo taxInfo,
        int additionalSeats = 0, int additionalStorageGb = 0,
        bool premiumAccessAddon = false) :
        base(org, plan, taxInfo, additionalSeats, additionalStorageGb, premiumAccessAddon)
    {
        Customer = customerId;
    }
}
