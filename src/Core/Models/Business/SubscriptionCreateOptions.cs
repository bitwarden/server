using Bit.Core.Entities;
using Bit.Core.Enums;
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

    public OrganizationSubscriptionOptionsBase(Organization org, IEnumerable<StaticStore.Plan> plans, TaxInfo taxInfo, int additionalSeats
        , int additionalStorageGb, bool premiumAccessAddon, int additionalSmSeats = 0, int additionalServiceAccount = 0)
    {
        Items = new List<SubscriptionItemOptions>();
        Metadata = new Dictionary<string, string>
        {
            [org.GatewayIdField()] = org.Id.ToString()
        };

        foreach (var plan in plans)
        {
            if (plan.StripePlanId != null)
            {
                Items.Add(new SubscriptionItemOptions
                {
                    Plan = plan.StripePlanId,
                    Quantity = 1
                });
            }

            if (additionalSeats > 0 && plan.StripeSeatPlanId != null && plan.BitwardenProduct == BitwardenProductType.PasswordManager)
            {
                Items.Add(new SubscriptionItemOptions
                {
                    Plan = plan.StripeSeatPlanId,
                    Quantity = additionalSeats
                });
            }

            if (additionalStorageGb > 0 && plan.BitwardenProduct == BitwardenProductType.PasswordManager)
            {
                Items.Add(new SubscriptionItemOptions
                {
                    Plan = plan.StripeStoragePlanId,
                    Quantity = additionalStorageGb
                });
            }

            if (additionalSmSeats > 0 && plan.StripePlanId != null && plan.BitwardenProduct == BitwardenProductType.SecretsManager)
            {
                Items.Add(new SubscriptionItemOptions
                {
                    Plan = plan.StripePlanId,
                    Quantity = additionalSmSeats
                });
            }

            if (additionalServiceAccount > 0 && plan.StripePlanId != null && plan.BitwardenProduct == BitwardenProductType.SecretsManager)
            {
                Items.Add(new SubscriptionItemOptions
                {
                    Plan = plan.StripePlanId,
                    Quantity = additionalServiceAccount
                });
            }

            if (premiumAccessAddon && plan.StripePremiumAccessPlanId != null && plan.BitwardenProduct == BitwardenProductType.PasswordManager)
            {
                Items.Add(new SubscriptionItemOptions
                {
                    Plan = plan.StripePremiumAccessPlanId,
                    Quantity = 1
                });
            }
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

    public OrganizationPurchaseSubscriptionOptions(Organization org, IEnumerable<StaticStore.Plan> plans, TaxInfo taxInfo, int additionalSeats = 0
        , int additionalStorageGb = 0, bool premiumAccessAddon = false
        , int additionalSmSeats = 0, int additionalServiceAccount = 0) :
        base(org, plans, taxInfo, additionalSeats, additionalStorageGb, premiumAccessAddon, additionalSmSeats, additionalServiceAccount)
    {
        OffSession = true;
        TrialPeriodDays = plans.FirstOrDefault().TrialPeriodDays;
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
