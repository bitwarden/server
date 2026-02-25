using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

[SutProviderCustomize]
public class SendPolicyRequirementFactoryTests
{
    [Theory, BitAutoData]
    public void DisableSend_IsFalse_IfNoPolicies(SutProvider<SendPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.False(actual.DisableSend);
    }

    [Theory, BitAutoData]
    public void DisableSend_IsFalse_IfNotConfigured(
        [PolicyDetails(PolicyType.SendOptions)] PolicyDetails[] policies,
        SutProvider<SendPolicyRequirementFactory> sutProvider
        )
    {
        policies[0].SetDataModel(new SendPolicyData { DisableSend = false });
        policies[1].SetDataModel(new SendPolicyData { DisableSend = false });

        var actual = sutProvider.Sut.Create(policies);

        Assert.False(actual.DisableSend);
    }

    [Theory, BitAutoData]
    public void DisableSend_IsTrue_IfAnyConfigured(
        [PolicyDetails(PolicyType.SendOptions)] PolicyDetails[] policies,
        SutProvider<SendPolicyRequirementFactory> sutProvider
        )
    {
        policies[0].SetDataModel(new SendPolicyData { DisableSend = true });
        policies[1].SetDataModel(new SendPolicyData { DisableSend = false });

        var actual = sutProvider.Sut.Create(policies);

        Assert.True(actual.DisableSend);
    }

    [Theory, BitAutoData]
    public void DisableHideEmail_IsFalse_IfNoPolicies(SutProvider<SendPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.False(actual.DisableHideEmail);
    }

    [Theory, BitAutoData]
    public void DisableHideEmail_IsFalse_IfNotConfigured(
        [PolicyDetails(PolicyType.SendOptions)] PolicyDetails[] policies,
        SutProvider<SendPolicyRequirementFactory> sutProvider
        )
    {
        policies[0].SetDataModel(new SendPolicyData { DisableHideEmail = false });
        policies[1].SetDataModel(new SendPolicyData { DisableHideEmail = false });

        var actual = sutProvider.Sut.Create(policies);

        Assert.False(actual.DisableHideEmail);
    }

    [Theory, BitAutoData]
    public void DisableHideEmail_IsTrue_IfAnyConfigured(
        [PolicyDetails(PolicyType.SendOptions)] PolicyDetails[] policies,
        SutProvider<SendPolicyRequirementFactory> sutProvider
        )
    {
        policies[0].SetDataModel(new SendPolicyData { DisableHideEmail = true });
        policies[1].SetDataModel(new SendPolicyData { DisableHideEmail = false });

        var actual = sutProvider.Sut.Create(policies);

        Assert.True(actual.DisableHideEmail);
    }

    [Theory, BitAutoData]
    public void DisableNoAuthSends_IsFalse_IfNoPolicies(SutProvider<SendPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.False(actual.DisableNoAuthSends);
    }

    [Theory, BitAutoData]
    public void DisableNoAuthSends_IsFalse_IfNotConfigured(
        [PolicyDetails(PolicyType.SendOptions)] PolicyDetails[] policies,
        SutProvider<SendPolicyRequirementFactory> sutProvider
        )
    {
        policies[0].SetDataModel(new SendPolicyData { DisableNoAuthSends = false });
        policies[1].SetDataModel(new SendPolicyData { DisableNoAuthSends = false });

        var actual = sutProvider.Sut.Create(policies);

        Assert.False(actual.DisableNoAuthSends);
    }

    [Theory, BitAutoData]
    public void DisableNoAuthSends_IsTrue_IfAnyConfigured(
        [PolicyDetails(PolicyType.SendOptions)] PolicyDetails[] policies,
        SutProvider<SendPolicyRequirementFactory> sutProvider
        )
    {
        policies[0].SetDataModel(new SendPolicyData { DisableNoAuthSends = true });
        policies[1].SetDataModel(new SendPolicyData { DisableNoAuthSends = false });

        var actual = sutProvider.Sut.Create(policies);

        Assert.True(actual.DisableNoAuthSends);
    }

    [Theory, BitAutoData]
    public void DisablePasswordSends_IsFalse_IfNoPolicies(SutProvider<SendPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.False(actual.DisablePasswordSends);
    }

    [Theory, BitAutoData]
    public void DisablePasswordSends_IsFalse_IfNotConfigured(
        [PolicyDetails(PolicyType.SendOptions)] PolicyDetails[] policies,
        SutProvider<SendPolicyRequirementFactory> sutProvider
        )
    {
        policies[0].SetDataModel(new SendPolicyData { DisablePasswordSends = false });
        policies[1].SetDataModel(new SendPolicyData { DisablePasswordSends = false });

        var actual = sutProvider.Sut.Create(policies);

        Assert.False(actual.DisablePasswordSends);
    }

    [Theory, BitAutoData]
    public void DisablePasswordSends_IsTrue_IfAnyConfigured(
        [PolicyDetails(PolicyType.SendOptions)] PolicyDetails[] policies,
        SutProvider<SendPolicyRequirementFactory> sutProvider
        )
    {
        policies[0].SetDataModel(new SendPolicyData { DisablePasswordSends = true });
        policies[1].SetDataModel(new SendPolicyData { DisablePasswordSends = false });

        var actual = sutProvider.Sut.Create(policies);

        Assert.True(actual.DisablePasswordSends);
    }

    [Theory, BitAutoData]
    public void DisableEmailVerifiedSends_IsFalse_IfNoPolicies(SutProvider<SendPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.False(actual.DisableEmailVerifiedSends);
    }

    [Theory, BitAutoData]
    public void DisableEmailVerifiedSends_IsFalse_IfNotConfigured(
        [PolicyDetails(PolicyType.SendOptions)] PolicyDetails[] policies,
        SutProvider<SendPolicyRequirementFactory> sutProvider
        )
    {
        policies[0].SetDataModel(new SendPolicyData { DisableEmailVerifiedSends = false });
        policies[1].SetDataModel(new SendPolicyData { DisableEmailVerifiedSends = false });

        var actual = sutProvider.Sut.Create(policies);

        Assert.False(actual.DisableEmailVerifiedSends);
    }

    [Theory, BitAutoData]
    public void DisableEmailVerifiedSends_IsTrue_IfAnyConfigured(
        [PolicyDetails(PolicyType.SendOptions)] PolicyDetails[] policies,
        SutProvider<SendPolicyRequirementFactory> sutProvider
        )
    {
        policies[0].SetDataModel(new SendPolicyData { DisableEmailVerifiedSends = true });
        policies[1].SetDataModel(new SendPolicyData { DisableEmailVerifiedSends = false });

        var actual = sutProvider.Sut.Create(policies);

        Assert.True(actual.DisableEmailVerifiedSends);
    }

    [Theory, BitAutoData]
    public void DisableSend_IsFalse_IfOnlyTwoAuthTypesDisabledAcrossOrgs(
        [PolicyDetails(PolicyType.SendOptions)] PolicyDetails[] policies,
        SutProvider<SendPolicyRequirementFactory> sutProvider
        )
    {
        policies[0].SetDataModel(new SendPolicyData { DisableNoAuthSends = true });
        policies[1].SetDataModel(new SendPolicyData { DisablePasswordSends = true });

        var actual = sutProvider.Sut.Create(policies);

        Assert.False(actual.DisableSend);
    }

    [Theory, BitAutoData]
    public void DisableSend_IsTrue_IfAllThreeAuthTypesDisabledAcrossOrgs(
        [PolicyDetails(PolicyType.SendOptions)] PolicyDetails[] policies,
        SutProvider<SendPolicyRequirementFactory> sutProvider
        )
    {
        policies[0].SetDataModel(new SendPolicyData { DisableNoAuthSends = true, DisablePasswordSends = true });
        policies[1].SetDataModel(new SendPolicyData { DisableEmailVerifiedSends = true });

        var actual = sutProvider.Sut.Create(policies);

        Assert.True(actual.DisableSend);
    }
}
