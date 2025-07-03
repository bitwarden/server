#nullable enable
using Bit.Core.Billing.Enums;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Billing.Extensions;

public static class PlanCollectionExtensions
{
    public static Plan? GetPlan(this IEnumerable<Plan> plans, PlanType planType)
    {
        return plans.FirstOrDefault(plan => plan.Type == planType);
    }
}
