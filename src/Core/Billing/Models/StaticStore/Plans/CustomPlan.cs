using Bit.Core.Billing.Enums;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Billing.Models.StaticStore.Plans;

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
