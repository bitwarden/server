// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Collections.Immutable;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Models.StaticStore.Plans;
using Bit.Core.Models.StaticStore;

namespace Bit.Core.Test.Billing;

/// <summary>
/// Provides static plan data for testing purposes.
/// This data is NOT used in production - production code uses the Pricing Service.
/// </summary>
public static class MockPlans
{
    static MockPlans()
    {
        Plans = new List<Plan>
        {
            new EnterprisePlan(true),
            new EnterprisePlan(false),
            new TeamsStarterPlan(),
            new TeamsPlan(true),
            new TeamsPlan(false),

            new Enterprise2023Plan(true),
            new Enterprise2023Plan(false),
            new Enterprise2020Plan(true),
            new Enterprise2020Plan(false),
            new TeamsStarterPlan2023(),
            new Teams2023Plan(true),
            new Teams2023Plan(false),
            new Teams2020Plan(true),
            new Teams2020Plan(false),
            new FamiliesPlan(),
            new FreePlan(),
            new CustomPlan(),

            new Enterprise2019Plan(true),
            new Enterprise2019Plan(false),
            new Teams2019Plan(true),
            new Teams2019Plan(false),
            new Families2019Plan(),
        }.ToImmutableList();
    }

    /// <summary>
    /// Static list of test plan data.
    /// </summary>
    public static IEnumerable<Plan> Plans { get; }

    /// <summary>
    /// Retrieve a test plan by its type.
    /// </summary>
    /// <param name="planType">The type of plan to retrieve.</param>
    /// <returns>A plan matching the specified type, or null if not found.</returns>
    public static Plan GetPlan(PlanType planType) => Plans.SingleOrDefault(p => p.Type == planType);
}
