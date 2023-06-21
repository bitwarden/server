using Bit.Core.Entities;
using Bit.Core.Enums;
using Stripe;

namespace Bit.Core.Models.Business;

public class OrganizationSubscriptionOptionsBase : Stripe.SubscriptionCreateOptions
{
    public OrganizationSubscriptionOptionsBase(Organization org, List<StaticStore.Plan> plans, TaxInfo taxInfo, int additionalSeats,
        int additionalStorageGb, bool premiumAccessAddon, int additionalSmSeats = 0, int additionalServiceAccount = 0)
    {
        Items = new List<SubscriptionItemOptions>();
        Metadata = new Dictionary<string, string>
        {
            [org.GatewayIdField()] = org.Id.ToString()
        };
        foreach (var plan in plans)
        {
            switch (plan.BitwardenProduct)
            {
                case BitwardenProductType.PasswordManager:
                    {
                        AddPlanIdToSubscription(plan);
                        AddAdditionalSeatToSubscription(additionalSeats, plan);

                        AddAdditionalStorage(additionalStorageGb, plan);

                        AddPremiumAccessAddon(premiumAccessAddon, plan);

                        break;
                    }
                case BitwardenProductType.SecretsManager:
                    {
                        AddPlanIdToSubscription(plan);
                        AddAdditionalSeatToSubscription(additionalSmSeats, plan);

                        AddServiceAccount(additionalServiceAccount, plan);

                        AddPremiumAccessAddon(premiumAccessAddon, plan);

                        break;
                    }
            }
        }

        if (!string.IsNullOrWhiteSpace(taxInfo?.StripeTaxRateId))
        {
            DefaultTaxRates = new List<string> { taxInfo.StripeTaxRateId };
        }
    }

    private void AddServiceAccount(int additionalServiceAccount, StaticStore.Plan plan)
    {
        if (additionalServiceAccount > 0 && plan.StripeServiceAccountPlanId != null)
        {
            Items.Add(new SubscriptionItemOptions
            {
                Plan = plan.StripeServiceAccountPlanId,
                Quantity = additionalServiceAccount
            });
        }
    }

    private void AddAdditionalStorage(int additionalStorageGb, StaticStore.Plan plan)
    {
        if (additionalStorageGb > 0)
        {
            Items.Add(new SubscriptionItemOptions
            {
                Plan = plan.StripeStoragePlanId,
                Quantity = additionalStorageGb
            });
        }
    }

    private void AddPremiumAccessAddon(bool premiumAccessAddon, StaticStore.Plan plan)
    {
        if (premiumAccessAddon && plan.StripePremiumAccessPlanId != null)
        {
            Items.Add(new SubscriptionItemOptions { Plan = plan.StripePremiumAccessPlanId, Quantity = 1 });
        }
    }

    private void AddAdditionalSeatToSubscription(int additionalSeats, StaticStore.Plan plan)
    {
        if (additionalSeats > 0 && plan.StripeSeatPlanId != null)
        {
            Items.Add(new SubscriptionItemOptions { Plan = plan.StripeSeatPlanId, Quantity = additionalSeats });
        }
    }

    private void AddPlanIdToSubscription(StaticStore.Plan plan)
    {
        if (plan.StripePlanId != null)
        {
            Items.Add(new SubscriptionItemOptions { Plan = plan.StripePlanId, Quantity = 1 });
        }
    }
}

public class OrganizationPurchaseSubscriptionOptions : OrganizationSubscriptionOptionsBase
{
    public OrganizationPurchaseSubscriptionOptions(
        Organization org, List<StaticStore.Plan> plans,
        TaxInfo taxInfo, int additionalSeats = 0,
        int additionalStorageGb = 0, bool premiumAccessAddon = false,
        int additionalSmSeats = 0, int additionalServiceAccount = 0) :
        base(org, plans, taxInfo, additionalSeats, additionalStorageGb, premiumAccessAddon, additionalSmSeats, additionalServiceAccount)
    {
        OffSession = true;
        TrialPeriodDays = plans.FirstOrDefault(x => x.BitwardenProduct == BitwardenProductType.PasswordManager)!.TrialPeriodDays;
    }
}

public class OrganizationUpgradeSubscriptionOptions : OrganizationSubscriptionOptionsBase
{
    public OrganizationUpgradeSubscriptionOptions(
        string customerId, Organization org,
        List<StaticStore.Plan> plans, TaxInfo taxInfo,
        int additionalSeats = 0, int additionalStorageGb = 0,
        bool premiumAccessAddon = false) :
        base(org, plans, taxInfo, additionalSeats, additionalStorageGb, premiumAccessAddon)
    {
        Customer = customerId;
    }
}
