using System.Collections.Generic;
using Bit.Core.Models.Table;

namespace Bit.Core.Models.Business
{
    public class SponsoredOrganizationSubscription
    {
        public const string OrganizationSponsorhipIdMetadataKey = "OrganizationSponsorshipId";
        private readonly string _customerId;
        private readonly Organization _org;
        private readonly StaticStore.Plan _plan;
        private readonly List<Stripe.TaxRate> _taxRates;

        public SponsoredOrganizationSubscription(Organization org, Stripe.Subscription existingSubscription)
        {
            _org = org;
            _customerId = org.GatewayCustomerId;
            _plan = Utilities.StaticStore.GetPlan(org.PlanType);
            _taxRates = existingSubscription.DefaultTaxRates;
        }

        public SponsorOrganizationSubscriptionOptions GetSponsorSubscriptionOptions(OrganizationSponsorship sponsorship,
            int additionalSeats = 0, int additionalStorageGb = 0, bool premiumAccessAddon = false)
        {
            var sponsoredPlan = Utilities.StaticStore.GetSponsoredPlan(sponsorship.PlanSponsorshipType.Value);

            var subCreateOptions = new SponsorOrganizationSubscriptionOptions(_customerId, _org, _plan,
                sponsoredPlan, _taxRates, additionalSeats, additionalStorageGb, premiumAccessAddon);

            subCreateOptions.Metadata.Add(OrganizationSponsorhipIdMetadataKey, sponsorship.Id.ToString());
            return subCreateOptions;
        }

        public OrganizationUpgradeSubscriptionOptions RemoveOrganizationSubscriptionOptions(int additionalSeats = 0,
            int additionalStorageGb = 0, bool premiumAccessAddon = false) =>
            new OrganizationUpgradeSubscriptionOptions(_customerId, _org, _plan, _taxRates,
                additionalSeats, additionalStorageGb, premiumAccessAddon);
    }
}
