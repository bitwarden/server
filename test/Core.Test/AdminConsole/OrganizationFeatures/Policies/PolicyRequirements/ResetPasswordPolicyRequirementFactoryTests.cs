using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

[SutProviderCustomize]
public class ResetPasswordPolicyRequirementFactoryTests
{
    [Theory, BitAutoData]
    public void AutoEnrollAdministration_WithNoPolicies_ReturnsFalse(SutProvider<ResetPasswordPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.False(actual.AutoEnrollEnabled);
    }

    [Theory, BitAutoData]
    public void AutoEnrollAdministration_WithResetPasswordPolices_ReturnsTrue(
        [PolicyDetails(PolicyType.ResetPassword)] PolicyDetails[] policies,
        SutProvider<ResetPasswordPolicyRequirementFactory> sutProvider)
    {
        policies[0].SetDataModel(new ResetPasswordDataModel { AutoEnrollEnabled = true });
        policies[1].SetDataModel(new ResetPasswordDataModel { AutoEnrollEnabled = false });

        var actual = sutProvider.Sut.Create(policies);

        Assert.True(actual.AutoEnrollEnabled);
    }
}
