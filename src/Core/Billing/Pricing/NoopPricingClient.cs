using Bit.Core.Billing.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Billing.Pricing;

/// <summary>
/// Registered for self-hosted instances, which have no Pricing Service to call. Mirrors the
/// pre-split behavior where every <see cref="IPricingClient"/> method short-circuited to a
/// no-data result when <c>GlobalSettings.SelfHosted</c> was true.
/// </summary>
internal sealed class NoopPricingClient : IPricingClient
{
    public Task<Plan?> GetPlan(PlanType planType) => Task.FromResult<Plan?>(null);

    public Task<Plan> GetPlanOrThrow(PlanType planType) =>
        throw new NotFoundException($"Could not find plan for type {planType}");

    public Task<List<Plan>> ListPlans() => Task.FromResult(new List<Plan>());

    public Task<Premium.Plan> GetAvailablePremiumPlan() =>
        throw new NotFoundException("Could not find available premium plan");

    public Task<List<Premium.Plan>> ListPremiumPlans() => Task.FromResult(new List<Premium.Plan>());
}
