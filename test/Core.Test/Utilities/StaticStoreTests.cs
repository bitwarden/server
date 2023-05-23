using Bit.Core.Enums;
using Bit.Core.Utilities;
using Bit.Core.Models.StaticStore;

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
    public void StaticStore_GetPasswordManagerPlanByPlanType_Success(PlanType planType)
    {
        var plan = StaticStore.GetPasswordManagerPlan(planType);

        Assert.NotNull(plan);
        Assert.Equal(planType, plan.Type);
    }

    [Theory]
    [InlineData(PlanType.EnterpriseAnnually)]
    public void StaticStore_GetSecretsManagerPlanByPlanType_Success(PlanType planType)
    {
        var plan = StaticStore.GetSecretsManagerPlan(planType);

        Assert.NotNull(plan);
        Assert.Equal(planType, plan.Type);
    }

    [Theory]
    [InlineData(PlanType.EnterpriseAnnually)]
    public void StaticStore_GetPasswordManagerPlan_ReturnsPasswordManagerPlans(PlanType planType)
    {
        var plan = StaticStore.GetPasswordManagerPlan(planType);
        Assert.NotNull(plan);
        Assert.Equal(BitwardenProductType.PasswordManager, plan.BitwardenProduct);
    }

    [Theory]
    [InlineData(PlanType.EnterpriseAnnually)]
    public void StaticStore_GetSecretsManagerPlan_ReturnsSecretManagerPlans(PlanType planType)
    {
        var plan = StaticStore.GetSecretsManagerPlan(planType);
        Assert.NotNull(plan);
        Assert.Equal(BitwardenProductType.SecretsManager, plan.BitwardenProduct);
    }

    [Theory]
    [InlineData(PlanType.EnterpriseAnnually, BitwardenProductType.PasswordManager)]
    public void StaticStore_AddDuplicatePlans_SingleOrDefaultThrowsException(PlanType planType, BitwardenProductType bitwardenProductType)
    {
        var plansStore = new List<Plan>
        {
            new Plan { Type = PlanType.EnterpriseAnnually, BitwardenProduct = BitwardenProductType.PasswordManager },
            new Plan { Type = PlanType.EnterpriseAnnually, BitwardenProduct = BitwardenProductType.PasswordManager }
        };

        Assert.Throws<InvalidOperationException>(() => plansStore.SingleOrDefault(p => p.Type == planType && p.BitwardenProduct == bitwardenProductType));
    }
}
