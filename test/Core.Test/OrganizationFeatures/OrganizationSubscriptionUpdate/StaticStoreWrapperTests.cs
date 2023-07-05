using Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSubscriptionUpdate;

public class StaticStoreWrapperTests
{
    [Theory]
    [BitAutoData]
    public void StaticStoreWrapper_SecretManagerPlans_InitializedCorrectly()
    {
        var expectedPlans = StaticStore.SecretManagerPlans.ToList();

        var staticStoreWrapper = new StaticStoreWrapper();
        var actualPlans = staticStoreWrapper.SecretsManagerPlans;

        Assert.Equal(expectedPlans.Count, actualPlans.Count);
        for (var i = 0; i < expectedPlans.Count; i++)
        {
            Assert.Equal(expectedPlans[i].Type, actualPlans[i].Type);
            Assert.Equal(expectedPlans[i].Name, actualPlans[i].Name);
            Assert.Equal(expectedPlans[i].BitwardenProduct, actualPlans[i].BitwardenProduct);
            Assert.Equal(expectedPlans[i].StripeSeatPlanId, actualPlans[i].StripeSeatPlanId);
            Assert.Equal(expectedPlans[i].StripeServiceAccountPlanId, actualPlans[i].StripeServiceAccountPlanId);
            Assert.Equal(expectedPlans[i].HasAdditionalStorageOption, actualPlans[i].HasAdditionalStorageOption);
            Assert.Equal(expectedPlans[i].HasAdditionalServiceAccountOption, actualPlans[i].HasAdditionalServiceAccountOption);
        }
    }
}
