using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

[SutProviderCustomize]
public class DisableSendPolicyRequirementFactoryTests
{
    [Theory, BitAutoData]
    public void DisableSend_IsFalse_IfNoPolicies(SutProvider<DisableSendPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.False(actual.DisableSend);
    }

    [Theory, BitAutoData]
    public void DisableSend_IsTrue_IfAnyDisableSendPolicies(
        [PolicyDetails(PolicyType.DisableSend)] PolicyDetails[] policies,
        SutProvider<DisableSendPolicyRequirementFactory> sutProvider
        )
    {
        var actual = sutProvider.Sut.Create(policies);

        Assert.True(actual.DisableSend);
    }
}
