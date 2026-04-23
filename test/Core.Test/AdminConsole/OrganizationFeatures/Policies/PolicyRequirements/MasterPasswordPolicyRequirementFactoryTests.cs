using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

[SutProviderCustomize]
public class MasterPasswordPolicyRequirementFactoryTests
{
    [Theory, BitAutoData]
    public void Create_WithNoPolicies_EnforcedOptionsIsNull(SutProvider<MasterPasswordPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.Null(actual.EnforcedOptions);
    }

    [Theory, BitAutoData]
    public void Create_WithAllNullOptions_EnforcedOptionsIsNull(
        [PolicyDetails(PolicyType.MasterPassword)] PolicyDetails[] policies,
        SutProvider<MasterPasswordPolicyRequirementFactory> sutProvider)
    {
        foreach (var policy in policies)
        {
            policy.SetDataModel(new MasterPasswordPolicyData());
        }

        var actual = sutProvider.Sut.Create(policies);

        Assert.Null(actual.EnforcedOptions);
    }

    [Theory, BitAutoData]
    public void Create_WithSinglePolicy_ReturnsEnforcedOptions(
        [PolicyDetails(PolicyType.MasterPassword)] PolicyDetails[] policies,
        SutProvider<MasterPasswordPolicyRequirementFactory> sutProvider)
    {
        policies[0].SetDataModel(new MasterPasswordPolicyData { MinLength = 12, RequireUpper = true });

        var actual = sutProvider.Sut.Create([policies[0]]);

        Assert.NotNull(actual.EnforcedOptions);
        Assert.Equal(12, actual.EnforcedOptions.MinLength);
        Assert.True(actual.EnforcedOptions.RequireUpper);
    }

    [Theory, BitAutoData]
    public void Create_WithMultiplePolicies_TakesMaxMinComplexity(
        [PolicyDetails(PolicyType.MasterPassword)] PolicyDetails[] policies,
        SutProvider<MasterPasswordPolicyRequirementFactory> sutProvider)
    {
        policies[0].SetDataModel(new MasterPasswordPolicyData { MinComplexity = 2 });
        policies[1].SetDataModel(new MasterPasswordPolicyData { MinComplexity = 4 });

        var actual = sutProvider.Sut.Create([policies[0], policies[1]]);

        Assert.NotNull(actual.EnforcedOptions);
        Assert.Equal(4, actual.EnforcedOptions.MinComplexity);
    }

    [Theory, BitAutoData]
    public void Create_WithMultiplePolicies_TakesMaxMinLength(
        [PolicyDetails(PolicyType.MasterPassword)] PolicyDetails[] policies,
        SutProvider<MasterPasswordPolicyRequirementFactory> sutProvider)
    {
        policies[0].SetDataModel(new MasterPasswordPolicyData { MinLength = 12 });
        policies[1].SetDataModel(new MasterPasswordPolicyData { MinLength = 20 });

        var actual = sutProvider.Sut.Create([policies[0], policies[1]]);

        Assert.NotNull(actual.EnforcedOptions);
        Assert.Equal(20, actual.EnforcedOptions.MinLength);
    }

    [Theory, BitAutoData]
    public void Create_WithMultiplePolicies_OrsAllBooleanFlags(
        [PolicyDetails(PolicyType.MasterPassword)] PolicyDetails[] policies,
        SutProvider<MasterPasswordPolicyRequirementFactory> sutProvider)
    {
        policies[0].SetDataModel(new MasterPasswordPolicyData { RequireUpper = true });
        policies[1].SetDataModel(new MasterPasswordPolicyData { RequireSpecial = true });

        var actual = sutProvider.Sut.Create([policies[0], policies[1]]);

        Assert.NotNull(actual.EnforcedOptions);
        Assert.True(actual.EnforcedOptions.RequireUpper);
        Assert.True(actual.EnforcedOptions.RequireSpecial);
    }
}
