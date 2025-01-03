using Bit.Core.Billing.Enums;
using Bit.Core.Models.StaticStore;

#nullable enable

namespace Bit.Core.Billing.Pricing;

public interface IPricingClient
{
    Task<Plan?> GetPlan(PlanType planType);
    Task<List<Plan>> ListPlans();
}
