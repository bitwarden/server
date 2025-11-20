using Bit.Core.Billing.Enums;
using Bit.Core.Models.StaticStore;
using Bit.Core.Test.Billing.Mocks.Plans;

namespace Bit.Core.Test.Billing.Mocks;

public class MockPlans
{
    public static List<Plan> Plans =>
    [
        new CustomPlan(),
        new Enterprise2019Plan(false),
        new Enterprise2019Plan(true),
        new Enterprise2020Plan(false),
        new Enterprise2020Plan(true),
        new Enterprise2023Plan(false),
        new Enterprise2023Plan(true),
        new EnterprisePlan(false),
        new EnterprisePlan(true),
        new Families2019Plan(),
        new Families2025Plan(),
        new FamiliesPlan(),
        new FreePlan(),
        new Teams2019Plan(false),
        new Teams2019Plan(true),
        new Teams2020Plan(false),
        new Teams2020Plan(true),
        new Teams2023Plan(false),
        new Teams2023Plan(true),
        new TeamsPlan(false),
        new TeamsPlan(true),
        new TeamsStarterPlan(),
        new TeamsStarterPlan2023()
    ];

    public static Plan Get(PlanType planType) => Plans.SingleOrDefault(p => p.Type == planType)!;
}
