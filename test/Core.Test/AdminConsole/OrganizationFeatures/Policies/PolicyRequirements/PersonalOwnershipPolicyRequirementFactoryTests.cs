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
    public void State_WithNoPolicies_ReturnsAllowed(SutProvider<PersonalOwnershipPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.Equal(PersonalOwnershipState.Allowed, actual.State);
    }

    [Theory, BitAutoData]
    public void State_WithPersonalOwnershipPolicies_ReturnsRestricted(
        [PolicyDetails(PolicyType.PersonalOwnership)] PolicyDetails[] policies,
        SutProvider<PersonalOwnershipPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(policies);

        Assert.Equal(PersonalOwnershipState.Restricted, actual.State);
    }

    [Theory, BitAutoData]
    public void RequiresDefaultCollection_WithNoPolicies_ReturnsFalse(
        Guid organizationId,
        SutProvider<PersonalOwnershipPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.False(actual.RequiresDefaultCollection(organizationId));
    }

    [Theory, BitAutoData]
    public void RequiresDefaultCollection_WithPersonalOwnershipPolicies_ReturnsCorrectResult(
        [PolicyDetails(PolicyType.PersonalOwnership)] PolicyDetails[] policies,
        Guid nonPolicyOrganizationId,
        SutProvider<PersonalOwnershipPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(policies);

        Assert.True(actual.RequiresDefaultCollection(policies[0].OrganizationId));
        Assert.False(actual.RequiresDefaultCollection(nonPolicyOrganizationId));
    }
}
