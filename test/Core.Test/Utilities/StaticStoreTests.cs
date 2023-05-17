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

    [Fact]
    public void StaticStore_GetPlanByPlanType_Success()
    {
        var plan = StaticStore.GetPlan(PlanType.EnterpriseAnnually);

        Assert.NotNull(plan);
        Assert.Equal(PlanType.EnterpriseAnnually,plan.Type);
    }

    [Fact]
    public void StaticStore_GetPlanPlanTypeOnly_ReturnsPasswordManagerPlans()
    {
        var plan = StaticStore.GetPlan(PlanType.EnterpriseAnnually);
        Assert.NotNull(plan);
        Assert.Equal(plan.BitwardenProduct, BitwardenProductType.PasswordManager);
    }

    [Fact]
    public void StaticStore_GetPlanPlanTypBitwardenProductType_ReturnsSecretManagerPlans()
    {
        var plan = StaticStore.GetPlan(PlanType.EnterpriseAnnually, BitwardenProductType.SecretManager);
        Assert.NotNull(plan);
        Assert.Equal(BitwardenProductType.SecretManager,plan.BitwardenProduct);
    }
}
