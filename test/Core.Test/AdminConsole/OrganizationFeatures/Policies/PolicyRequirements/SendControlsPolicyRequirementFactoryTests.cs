using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

[SutProviderCustomize]
public class SendControlsPolicyRequirementFactoryTests
{
    [Theory, BitAutoData]
    public void DisableSend_IsFalse_IfNoPolicies(SutProvider<SendControlsPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.False(actual.DisableSend);
    }

    [Theory, BitAutoData]
    public void DisableSend_IsFalse_WhenNotConfigured(
        [PolicyDetails(PolicyType.SendControls)] PolicyDetails[] policies,
        SutProvider<SendControlsPolicyRequirementFactory> sutProvider)
    {
        foreach (var policy in policies)
        {
            policy.SetDataModel(new SendControlsPolicyData { DisableSend = false, DisableHideEmail = false });
        }

        var actual = sutProvider.Sut.Create(policies);

        Assert.False(actual.DisableSend);
    }

    [Theory, BitAutoData]
    public void DisableSend_IsTrue_IfAnyPolicyHasDisableSend(
        [PolicyDetails(PolicyType.SendControls)] PolicyDetails[] policies,
        SutProvider<SendControlsPolicyRequirementFactory> sutProvider)
    {
        policies[0].SetDataModel(new SendControlsPolicyData { DisableSend = true, DisableHideEmail = false });
        policies[1].SetDataModel(new SendControlsPolicyData { DisableSend = false, DisableHideEmail = false });

        var actual = sutProvider.Sut.Create(policies);

        Assert.True(actual.DisableSend);
    }

    [Theory, BitAutoData]
    public void DisableHideEmail_IsFalse_IfNoPolicies(SutProvider<SendControlsPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.False(actual.DisableHideEmail);
    }

    [Theory, BitAutoData]
    public void DisableHideEmail_IsFalse_WhenNotConfigured(
        [PolicyDetails(PolicyType.SendControls)] PolicyDetails[] policies,
        SutProvider<SendControlsPolicyRequirementFactory> sutProvider)
    {
        foreach (var policy in policies)
        {
            policy.SetDataModel(new SendControlsPolicyData { DisableSend = false, DisableHideEmail = false });
        }

        var actual = sutProvider.Sut.Create(policies);

        Assert.False(actual.DisableHideEmail);
    }

    [Theory, BitAutoData]
    public void DisableHideEmail_IsTrue_IfAnyPolicyHasDisableHideEmail(
        [PolicyDetails(PolicyType.SendControls)] PolicyDetails[] policies,
        SutProvider<SendControlsPolicyRequirementFactory> sutProvider)
    {
        policies[0].SetDataModel(new SendControlsPolicyData { DisableSend = false, DisableHideEmail = true });
        policies[1].SetDataModel(new SendControlsPolicyData { DisableSend = false, DisableHideEmail = false });

        var actual = sutProvider.Sut.Create(policies);

        Assert.True(actual.DisableHideEmail);
    }

    [Theory, BitAutoData]
    public void BothFields_AreOrAggregatedAcrossMultiplePolicies(
        [PolicyDetails(PolicyType.SendControls)] PolicyDetails[] policies,
        SutProvider<SendControlsPolicyRequirementFactory> sutProvider)
    {
        policies[0].SetDataModel(new SendControlsPolicyData { DisableSend = true, DisableHideEmail = false });
        policies[1].SetDataModel(new SendControlsPolicyData { DisableSend = false, DisableHideEmail = true });

        var actual = sutProvider.Sut.Create(policies);

        Assert.True(actual.DisableSend);
        Assert.True(actual.DisableHideEmail);
    }
}
