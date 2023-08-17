using Bit.Core.Enums;

namespace Bit.Core.Utilities.Plan;

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
            AllowSeatAutoscale = false;
        }
    }
}
