using Bit.Core.Billing.Constants;

namespace Bit.Core.Test.Billing.Mocks.Plans;

using PersonalPremiumPlan = Core.Billing.Pricing.Premium.Plan;

public class PremiumPlan : PersonalPremiumPlan
{
    public PremiumPlan()
    {
        Name = "Premium";
        Available = true;
        LegacyYear = null;
        Seat = new Bit.Core.Billing.Pricing.Premium.Purchasable
        {
            Price = 10M,
            StripePriceId = StripeConstants.Prices.PremiumAnnually,
            Provided = 0
        };
        Storage = new Bit.Core.Billing.Pricing.Premium.Purchasable
        {
            Price = 4M,
            StripePriceId = StripeConstants.Prices.StoragePlanPersonal,
            Provided = 1
        };
    }
}
