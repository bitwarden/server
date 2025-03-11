using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Enums;
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
    public void DisablePersonalOwnership_WithNonPersonalOwnershipPolicies_ReturnsFalse(
        [PolicyDetails(PolicyType.RequireSso)] PolicyDetails otherPolicy1,
        [PolicyDetails(PolicyType.SendOptions)] PolicyDetails otherPolicy2,
        SutProvider<PersonalOwnershipPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([otherPolicy1, otherPolicy2]);

        Assert.False(actual.DisablePersonalOwnership);
    }

    [Theory, BitAutoData]
    public void DisablePersonalOwnership_WithPersonalOwnershipPolicies_ReturnsTrue(
        [PolicyDetails(PolicyType.PersonalOwnership)] PolicyDetails[] policies,
        SutProvider<PersonalOwnershipPolicyRequirementFactory> sutProvider
        )
    {
        var actual = sutProvider.Sut.Create(policies);

        Assert.True(actual.DisablePersonalOwnership);
    }

    [Theory, BitAutoData]
    public void DisablePersonalOwnership_WithProviderUserParameter_ReturnsFalse(
        [PolicyDetails(PolicyType.PersonalOwnership, isProvider: true)] PolicyDetails policyDetails,
        SutProvider<PersonalOwnershipPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([policyDetails]);

        Assert.False(actual.DisablePersonalOwnership);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner, false)]
    [BitAutoData(OrganizationUserType.Admin, false)]
    [BitAutoData(OrganizationUserType.User, true)]
    [BitAutoData(OrganizationUserType.Custom, true)]
    public void DisablePersonalOwnership_RespectsExemptRoles(
        OrganizationUserType userType,
        bool shouldBeEnforced,
        [PolicyDetails(PolicyType.PersonalOwnership)] PolicyDetails policyDetails,
        SutProvider<PersonalOwnershipPolicyRequirementFactory> sutProvider)
    {
        policyDetails.OrganizationUserType = userType;

        var actual = sutProvider.Sut.Create([policyDetails]);

        Assert.Equal(shouldBeEnforced, actual.DisablePersonalOwnership);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Confirmed, true)]
    [BitAutoData(OrganizationUserStatusType.Accepted, true)]
    [BitAutoData(OrganizationUserStatusType.Invited, false)]
    [BitAutoData(OrganizationUserStatusType.Revoked, false)]
    public void DisablePersonalOwnership_RespectsExemptStatuses(
        OrganizationUserStatusType userStatus,
        bool shouldBeEnforced,
        [PolicyDetails(PolicyType.PersonalOwnership)] PolicyDetails policyDetails,
        SutProvider<PersonalOwnershipPolicyRequirementFactory> sutProvider)
    {
        policyDetails.OrganizationUserStatus = userStatus;

        var actual = sutProvider.Sut.Create([policyDetails]);

        Assert.Equal(shouldBeEnforced, actual.DisablePersonalOwnership);
    }
}
