using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

[SutProviderCustomize]
public class SendOptionsPolicyRequirementFactoryTests
{
    [Theory, BitAutoData]
    public void DisableHideEmail_IsFalse_IfNoPolicies(SutProvider<SendOptionsPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.False(actual.DisableHideEmail);
    }

    [Theory, BitAutoData]
    public void DisableHideEmail_IsFalse_IfNotConfigured(
        [PolicyDetails(PolicyType.SendOptions)] PolicyDetails[] policies,
        SutProvider<SendOptionsPolicyRequirementFactory> sutProvider
        )
    {
        policies[0].SetDataModel(new SendOptionsPolicyData { DisableHideEmail = false });
        policies[1].SetDataModel(new SendOptionsPolicyData { DisableHideEmail = false });

        var actual = sutProvider.Sut.Create(policies);

        Assert.False(actual.DisableHideEmail);
    }

    [Theory, BitAutoData]
    public void DisableHideEmail_IsTrue_IfAnyConfigured(
        [PolicyDetails(PolicyType.SendOptions)] PolicyDetails[] policies,
        SutProvider<SendOptionsPolicyRequirementFactory> sutProvider
        )
    {
        policies[0].SetDataModel(new SendOptionsPolicyData { DisableHideEmail = true });
        policies[1].SetDataModel(new SendOptionsPolicyData { DisableHideEmail = false });

        var actual = sutProvider.Sut.Create(policies);

        Assert.True(actual.DisableHideEmail);
    }
}
