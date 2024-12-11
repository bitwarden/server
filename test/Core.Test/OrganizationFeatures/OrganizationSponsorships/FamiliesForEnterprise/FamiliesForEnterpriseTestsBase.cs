using Bit.Core.Billing.Enums;
using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSponsorships.FamiliesForEnterprise;

public abstract class FamiliesForEnterpriseTestsBase
{
    public static IEnumerable<object[]> EnterprisePlanTypes =>
        Enum.GetValues<PlanType>()
            .Where(p => StaticStore.GetPlan(p).ProductTier == ProductTierType.Enterprise)
            .Select(p => new object[] { p });

    public static IEnumerable<object[]> NonEnterprisePlanTypes =>
        Enum.GetValues<PlanType>()
            .Where(p => StaticStore.GetPlan(p).ProductTier != ProductTierType.Enterprise)
            .Select(p => new object[] { p });

    public static IEnumerable<object[]> FamiliesPlanTypes =>
        Enum.GetValues<PlanType>()
            .Where(p => StaticStore.GetPlan(p).ProductTier == ProductTierType.Families)
            .Select(p => new object[] { p });

    public static IEnumerable<object[]> NonFamiliesPlanTypes =>
        Enum.GetValues<PlanType>()
            .Where(p => StaticStore.GetPlan(p).ProductTier != ProductTierType.Families)
            .Select(p => new object[] { p });

    public static IEnumerable<object[]> NonConfirmedOrganizationUsersStatuses =>
        Enum.GetValues<OrganizationUserStatusType>()
            .Where(s => s != OrganizationUserStatusType.Confirmed)
            .Select(s => new object[] { s });
}
