using System.Collections.Generic;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Business
{
    public class SponsoredOrganizationSubscription
    {
        public const string OrganizationSponsorhipIdMetadataKey = "OrganizationSponsorshipId";
        private readonly string _customerId;
        private readonly Organization _org;
        private readonly OrganizationSponsorship _sponsorship;
        private readonly StaticStore.Plan _plan;
        private readonly StaticStore.SponsoredPlan _sponsoredPlan;
        private readonly List<Stripe.TaxRate> _taxRates;

        public SponsoredOrganizationSubscription(Organization org, OrganizationSponsorship sponsorship, Stripe.Subscription existingSubscription)
        {
            _org = org;
            _sponsorship = sponsorship;
            _customerId = org.GatewayCustomerId;
            _plan = Utilities.StaticStore.GetPlan(org.PlanType);
            _sponsoredPlan = Utilities.StaticStore.GetSponsoredPlan(sponsorship.PlanSponsorshipType.Value);
            _taxRates = existingSubscription.DefaultTaxRates;
        }

        public SponsorOrganizationSubscriptionOptions GetSponsorSubscriptionOptions(int additionalSeats = 0,
            int additionalStorageGb = 0, bool premiumAccessAddon = false)
        {
            var subCreateOptions = new SponsorOrganizationSubscriptionOptions(_customerId, _org, _plan,
                _sponsoredPlan, _taxRates, additionalSeats, additionalStorageGb, premiumAccessAddon);

            subCreateOptions.Metadata.Add(OrganizationSponsorhipIdMetadataKey, _sponsorship.Id.ToString());
            return subCreateOptions;
        }

        public OrganizationUpgradeSubscriptionOptions RemoveOrganizationSubscriptionOptions(int additionalSeats = 0,
            int additionalStorageGb = 0, bool premiumAccessAddon = false) =>
            new OrganizationUpgradeSubscriptionOptions(_customerId, _org, _plan, _taxRates,
                additionalSeats, additionalStorageGb, premiumAccessAddon);
    }
}
