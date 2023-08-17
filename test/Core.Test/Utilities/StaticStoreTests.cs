using Bit.Core.Enums;
using Bit.Core.Models.StaticStore;
using Bit.Core.Utilities;
using Xunit;

namespace Bit.Core.Test.Utilities;


public class StaticStoreTests
{
    [Fact]
    public void StaticStore_Initialization_Success()
    {
        var plans = StaticStore.Plans;
        Assert.NotNull(plans);
        Assert.NotEmpty(plans);
        Assert.Equal(11, plans.Count());
    }

    [Theory]
    [InlineData(PlanType.EnterpriseAnnually)]
    public void StaticStore_GetPasswordManagerPlanByPlanType_Success(PlanType planType)
    {
        var plan = StaticStore.GetPlan(planType);
        Assert.NotNull(plan);
        Assert.Equal(planType, plan.Type);
    }

    [Theory]
    [InlineData(PlanType.EnterpriseAnnually)]
    public void StaticStore_GetSecretsManagerPlanByPlanType_Success(PlanType planType)
    {
        var plan = StaticStore.GetPlan(planType);

        Assert.NotNull(plan);
        Assert.Equal(planType, plan.Type);
    }

    [Theory]
    [InlineData(PlanType.EnterpriseAnnually)]
    public void StaticStore_GetPasswordManagerPlan_ReturnsPasswordManagerPlans(PlanType planType)
    {
        var plan = StaticStore.GetPlan(planType);
        Assert.NotNull(plan);
        Assert.NotNull(plan.PasswordManager);
    }

    [Theory]
    [InlineData(PlanType.EnterpriseAnnually)]
    public void StaticStore_GetSecretsManagerPlan_ReturnsSecretManagerPlans(PlanType planType)
    {
        var plan = StaticStore.GetPlan(planType);
        Assert.NotNull(plan);
        Assert.NotNull(plan.SecretsManager);
    }
}
