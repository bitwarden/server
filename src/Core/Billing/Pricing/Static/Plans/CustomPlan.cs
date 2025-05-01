using Bit.Core.Billing.Pricing.Enums;

namespace Bit.Core.Billing.Pricing.Static.Plans;

public record CustomPlan : Plan
{
    public CustomPlan()
    {
        Type = PlanType.Custom;
        PasswordManager = new CustomPasswordManagerFeatures();
    }

    private record CustomPasswordManagerFeatures : PasswordManagerPlanFeatures
    {
        public CustomPasswordManagerFeatures()
        {
            AllowSeatAutoscale = true;
        }
    }
}
