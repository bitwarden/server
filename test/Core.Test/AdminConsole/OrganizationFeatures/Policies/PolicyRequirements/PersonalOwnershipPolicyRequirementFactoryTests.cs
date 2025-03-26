using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

[SutProviderCustomize]
public class PersonalOwnershipPolicyRequirementFactoryTests
{
    [Theory, BitAutoData]
    public void DisablePersonalOwnership_WithNoPolicies_ReturnsFalse(SutProvider<PersonalOwnershipPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.False(actual.DisablePersonalOwnership);
    }

    [Theory, BitAutoData]
    public void DisablePersonalOwnership_WithPersonalOwnershipPolicies_ReturnsTrue(
        [PolicyDetails(PolicyType.PersonalOwnership)] PolicyDetails[] policies,
        SutProvider<PersonalOwnershipPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(policies);

        Assert.True(actual.DisablePersonalOwnership);
    }
}
