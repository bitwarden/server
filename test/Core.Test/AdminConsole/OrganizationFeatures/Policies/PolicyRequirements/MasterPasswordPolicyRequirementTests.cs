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
    public void MasterPassword_IsFalse_IfNoPolicies(SutProvider<MasterPasswordPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.False(actual.Enabled);
        Assert.Null(actual.EnforcedOptions);
    }

    [Theory, BitAutoData]
    public void MasterPassword_IsTrue_IfAnyDisableSendPolicies(
        [PolicyDetails(PolicyType.MasterPassword)] PolicyDetails[] policies,
        SutProvider<MasterPasswordPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(policies);

        Assert.True(actual.Enabled);
        Assert.NotNull(actual.EnforcedOptions);
        Assert.NotNull(actual.EnforcedOptions.EnforceOnLogin);
        Assert.NotNull(actual.EnforcedOptions.RequireLower);
        Assert.NotNull(actual.EnforcedOptions.RequireNumbers);
        Assert.NotNull(actual.EnforcedOptions.RequireSpecial);
        Assert.NotNull(actual.EnforcedOptions.RequireUpper);
        Assert.Null(actual.EnforcedOptions.MinComplexity);
        Assert.Null(actual.EnforcedOptions.MinLength);
    }
}
