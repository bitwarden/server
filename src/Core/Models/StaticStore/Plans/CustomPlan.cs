using Bit.Core.Enums;

namespace Bit.Core.Models.StaticStore.Plans;

public record CustomPlan : Models.StaticStore.Plan
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
