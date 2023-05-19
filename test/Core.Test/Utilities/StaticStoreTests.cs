using Bit.Core.Enums;
using Bit.Core.Utilities;

namespace Bit.Core.Test.Utilities;
using Xunit;

public class StaticStoreTests
{
    [Fact]
    public void StaticStore_Initialization_Success()
    {
        var plans = StaticStore.Plans;
        Assert.NotNull(plans);
        Assert.NotEmpty(plans);
        Assert.Equal(17, plans.Count());
    }

    [Theory]
    [InlineData(PlanType.EnterpriseAnnually)]
    public void StaticStore_GetPlanByPlanType_Success(PlanType planType)
    {
        var plan = StaticStore.GetPlan(planType);

        Assert.NotNull(plan);
        Assert.Equal(planType, plan.Type);
    }

    [Theory]
    [InlineData(PlanType.EnterpriseAnnually, BitwardenProductType.PasswordManager)]
    public void StaticStore_GetPlanPlanTypeOnly_ReturnsPasswordManagerPlans(PlanType planType, BitwardenProductType bitwardenProductType)
    {
        var plan = StaticStore.GetPlan(planType);
        Assert.NotNull(plan);
        Assert.Equal(bitwardenProductType, plan.BitwardenProduct);
    }

    [Theory]
    [InlineData(PlanType.EnterpriseAnnually, BitwardenProductType.PasswordManager)]
    public void StaticStore_GetPlanPlanTypBitwardenProductType_ReturnsSecretManagerPlans(PlanType planType, BitwardenProductType bitwardenProductType)
    {
        var plan = StaticStore.GetPlan(planType, bitwardenProductType);
        Assert.NotNull(plan);
        Assert.Equal(bitwardenProductType, plan.BitwardenProduct);
    }
}
