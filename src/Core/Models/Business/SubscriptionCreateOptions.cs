using Bit.Core.Entities;
using Bit.Core.Enums;
using Stripe;
using Plan = Bit.Core.Models.StaticStore.Plan;

namespace Bit.Core.Models.Business;

public class OrganizationSubscriptionOptionsBase : Stripe.SubscriptionCreateOptions
{
    public OrganizationSubscriptionOptionsBase(Organization org, StaticStore.Plan plan, TaxInfo taxInfo, int additionalSeats,
        int additionalStorageGb, bool premiumAccessAddon, int additionalSmSeats, int additionalServiceAccounts)
    {
        Items = new List<SubscriptionItemOptions>();
        Metadata = new Dictionary<string, string>
        {
            [org.GatewayIdField()] = org.Id.ToString()
        };

        AddPlanIdToSubscription(plan);

        if (org.UseSecretsManager && plan.SupportsSecretsManager)
        {
            AddSecretsManagerSeat(plan, additionalSmSeats);
            AddServiceAccount(additionalServiceAccounts, plan);
        }

        AddPremiumAccessAddon(premiumAccessAddon, plan);
        AddPasswordManagerSeat(plan, additionalSeats);
        AddAdditionalStorage(additionalStorageGb, plan);

        if (!string.IsNullOrWhiteSpace(taxInfo?.StripeTaxRateId))
        {
            DefaultTaxRates = new List<string> { taxInfo.StripeTaxRateId };
        }
    }

    private void AddSecretsManagerSeat(Plan plan, int additionalSmSeats)
    {
        if (additionalSmSeats > 0 && plan.SecretsManager.StripeSeatPlanId != null)
        {
            Items.Add(new SubscriptionItemOptions
                { Plan = plan.SecretsManager.StripeSeatPlanId, Quantity = additionalSmSeats });
        }
    }

    private void AddPasswordManagerSeat(Plan plan, int additionalSeats)
    {
        if (additionalSeats > 0 && plan.PasswordManager.StripeSeatPlanId != null)
        {
            Items.Add(new SubscriptionItemOptions
                { Plan = plan.PasswordManager.StripeSeatPlanId, Quantity = additionalSeats });
        }
    }

    private void AddServiceAccount(int additionalServiceAccounts, StaticStore.Plan plan)
    {
        if (additionalServiceAccounts > 0 && plan.SecretsManager.StripeServiceAccountPlanId != null)
        {
            Items.Add(new SubscriptionItemOptions
            {
                Plan = plan.SecretsManager.StripeServiceAccountPlanId,
                Quantity = additionalServiceAccounts
            });
        }
    }

    private void AddAdditionalStorage(int additionalStorageGb, StaticStore.Plan plan)
    {
        if (additionalStorageGb > 0)
        {
            Items.Add(new SubscriptionItemOptions
            {
                Plan = plan.PasswordManager.StripeStoragePlanId,
                Quantity = additionalStorageGb
            });
        }
    }

    private void AddPremiumAccessAddon(bool premiumAccessAddon, StaticStore.Plan plan)
    {
        if (premiumAccessAddon && plan.PasswordManager.StripePremiumAccessPlanId != null)
        {
            Items.Add(new SubscriptionItemOptions { Plan = plan.PasswordManager.StripePremiumAccessPlanId, Quantity = 1 });
        }
    }

    private void AddPlanIdToSubscription(StaticStore.Plan plan)
    {
        if (plan.PasswordManager.StripePlanId != null)
        {
            Items.Add(new SubscriptionItemOptions { Plan = plan.PasswordManager.StripePlanId, Quantity = 1 });
        }
    }
}

public class OrganizationPurchaseSubscriptionOptions : OrganizationSubscriptionOptionsBase
{
    public OrganizationPurchaseSubscriptionOptions(
        Organization org, StaticStore.Plan plan,
        TaxInfo taxInfo, int additionalSeats,
        int additionalStorageGb, bool premiumAccessAddon,
        int additionalSmSeats, int additionalServiceAccounts) :
        base(org, plan, taxInfo, additionalSeats, additionalStorageGb, premiumAccessAddon, additionalSmSeats, additionalServiceAccounts)
    {
        OffSession = true;
        TrialPeriodDays = plan.TrialPeriodDays;
    }
}

public class OrganizationUpgradeSubscriptionOptions : OrganizationSubscriptionOptionsBase
{
    public OrganizationUpgradeSubscriptionOptions(
        string customerId, Organization org,
        StaticStore.Plan plan, OrganizationUpgrade upgrade) :
        base(org, plan, upgrade.TaxInfo, upgrade.AdditionalSeats, upgrade.AdditionalStorageGb,
        upgrade.PremiumAccessAddon, upgrade.AdditionalSmSeats.GetValueOrDefault(),
        upgrade.AdditionalServiceAccounts.GetValueOrDefault())
    {
        Customer = customerId;
    }
}
