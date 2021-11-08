using Bit.Core.Models.Table;
using Stripe;
using System.Collections.Generic;
using System.Linq;

namespace Bit.Core.Models.Business
{
    public class OrganizationSubscriptionOptionsBase : Stripe.SubscriptionCreateOptions
    {
        public OrganizationSubscriptionOptionsBase(Organization org, StaticStore.Plan plan,
            int additionalSeats, int additionalStorageGb, bool premiumAccessAddon)
        {
            Items = new List<SubscriptionItemOptions>();
            Metadata = new Dictionary<string, string>
            {
                [org.GatewayIdField()] = org.Id.ToString()
            };

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
        }

        protected void AddPlanItem(StaticStore.Plan plan) => AddPlanItem(plan.StripePlanId);
        protected void AddPlanItem(StaticStore.SponsoredPlan sponsoredPlan) => AddPlanItem(sponsoredPlan.StripePlanId);
        protected void AddPlanItem(string stripePlanId)
        {
            if (stripePlanId != null)
            {
                Items.Add(new SubscriptionItemOptions
                {
                    Plan = stripePlanId,
                    Quantity = 1,
                });
            }
        }

        protected void AddTaxRateItem(TaxInfo taxInfo) => AddTaxRateItem(new List<string> { taxInfo.StripeTaxRateId });
        protected void AddTaxRateItem(List<Stripe.TaxRate> taxRates) => AddTaxRateItem(taxRates?.Select(t => t.Id).ToList());
        protected void AddTaxRateItem(List<string> taxRateIds)
        {
            if (taxRateIds != null && taxRateIds.Any())
            {
                DefaultTaxRates = taxRateIds;
            }
        }
    }

    public abstract class UnsponsoredOrganizationSubscriptionOptionsBase : OrganizationSubscriptionOptionsBase
    {
        public UnsponsoredOrganizationSubscriptionOptionsBase(Organization org, StaticStore.Plan plan, TaxInfo taxInfo,
            int additionalSeats, int additionalStorage, bool premiumAccessAddon) :
            base(org, plan, additionalSeats, additionalStorage, premiumAccessAddon)
        {
            AddPlanItem(plan);
            AddTaxRateItem(taxInfo);
        }
        public UnsponsoredOrganizationSubscriptionOptionsBase(Organization org, StaticStore.Plan plan, List<Stripe.TaxRate> taxInfo,
            int additionalSeats, int additionalStorage, bool premiumAccessAddon) :
            base(org, plan, additionalSeats, additionalStorage, premiumAccessAddon)
        {
            AddPlanItem(plan);
            AddTaxRateItem(taxInfo);
        }

    }

    public class OrganizationPurchaseSubscriptionOptions : UnsponsoredOrganizationSubscriptionOptionsBase
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

    public class OrganizationUpgradeSubscriptionOptions : UnsponsoredOrganizationSubscriptionOptionsBase
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
        public OrganizationUpgradeSubscriptionOptions(
            string customerId, Organization org,
            StaticStore.Plan plan, List<Stripe.TaxRate> taxInfo,
            int additionalSeats = 0, int additionalStorageGb = 0,
            bool premiumAccessAddon = false) :
            base(org, plan, taxInfo, additionalSeats, additionalStorageGb, premiumAccessAddon)
        {
            Customer = customerId;
        }
    }

    public class RemoveOrganizationSubscriptionOptions : OrganizationSubscriptionOptionsBase
    {
        public RemoveOrganizationSubscriptionOptions(string customerId, Organization org,
            StaticStore.Plan plan, List<string> existingTaxRateStripeIds,
            int additionalSeats = 0, int additionalStorageGb = 0,
            bool premiumAccessAddon = false) :
            base(org, plan, additionalSeats, additionalStorageGb, premiumAccessAddon)
        {
            Customer = customerId;
            AddPlanItem(plan);
            AddTaxRateItem(existingTaxRateStripeIds);
        }

    }

    public class SponsorOrganizationSubscriptionOptions : OrganizationSubscriptionOptionsBase
    {
        public SponsorOrganizationSubscriptionOptions(
            string customerId, Organization org, StaticStore.Plan existingPlan,
            StaticStore.SponsoredPlan sponsorshipPlan, List<Stripe.TaxRate> existingTaxRates, int additionalSeats = 0,
            int additionalStorageGb = 0, bool premiumAccessAddon = false) :
            base(org, existingPlan, additionalSeats, additionalStorageGb, premiumAccessAddon)
        {
            Customer = customerId;
            AddPlanItem(sponsorshipPlan);
            AddTaxRateItem(existingTaxRates);
        }
    }
}
