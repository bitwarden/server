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
    public void AutoEnroll_WithNoPolicies_IsEmpty(SutProvider<ResetPasswordPolicyRequirementFactory> sutProvider, Guid orgId)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.False(actual.AutoEnrollEnabled(orgId));
    }

    [Theory, BitAutoData]
    public void AutoEnrollAdministration_WithAnyResetPasswordPolices_ReturnsEnabledOrganizationIds(
        [PolicyDetails(PolicyType.ResetPassword)] PolicyDetails[] policies,
        SutProvider<ResetPasswordPolicyRequirementFactory> sutProvider)
    {
        policies[0].SetDataModel(new ResetPasswordDataModel { AutoEnrollEnabled = true });
        policies[1].SetDataModel(new ResetPasswordDataModel { AutoEnrollEnabled = false });
        policies[2].SetDataModel(new ResetPasswordDataModel { AutoEnrollEnabled = true });

        var actual = sutProvider.Sut.Create(policies);

        Assert.True(actual.AutoEnrollEnabled(policies[0].OrganizationId));
        Assert.False(actual.AutoEnrollEnabled(policies[1].OrganizationId));
        Assert.True(actual.AutoEnrollEnabled(policies[2].OrganizationId));
    }
}
