using AutoFixture.Xunit2;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Enums;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public class PersonalOwnershipPolicyRequirementTests
{
    [Theory, AutoData]
    public void DisablePersonalOwnership_IsFalse_IfNoPersonalOwnershipPolicies(
        [PolicyDetails(PolicyType.RequireSso)] PolicyDetails otherPolicy1,
        [PolicyDetails(PolicyType.SendOptions)] PolicyDetails otherPolicy2)
    {
        var actual = PersonalOwnershipPolicyRequirement.Create([otherPolicy1, otherPolicy2]);

        Assert.False(actual.DisablePersonalOwnership);
    }

    [Theory]
    [InlineAutoData(OrganizationUserType.Owner, false)]
    [InlineAutoData(OrganizationUserType.Admin, false)]
    [InlineAutoData(OrganizationUserType.User, true)]
    [InlineAutoData(OrganizationUserType.Custom, true)]
    public void DisablePersonalOwnership_TestRoles(
        OrganizationUserType userType,
        bool shouldBeEnforced,
        [PolicyDetails(PolicyType.PersonalOwnership)] PolicyDetails policyDetails)
    {
        policyDetails.OrganizationUserType = userType;

        var actual = PersonalOwnershipPolicyRequirement.Create([policyDetails]);

        Assert.Equal(shouldBeEnforced, actual.DisablePersonalOwnership);
    }

    [Theory, AutoData]
    public void DisablePersonalOwnership_Not_EnforcedAgainstProviders(
        [PolicyDetails(PolicyType.PersonalOwnership, isProvider: true)] PolicyDetails policyDetails)
    {
        var actual = PersonalOwnershipPolicyRequirement.Create([policyDetails]);

        Assert.False(actual.DisablePersonalOwnership);
    }

    [Theory]
    [InlineAutoData(OrganizationUserStatusType.Confirmed, true)]
    [InlineAutoData(OrganizationUserStatusType.Accepted, true)]
    [InlineAutoData(OrganizationUserStatusType.Invited, false)]
    [InlineAutoData(OrganizationUserStatusType.Revoked, false)]
    public void DisablePersonalOwnership_TestStatuses(
        OrganizationUserStatusType userStatus,
        bool shouldBeEnforced,
        [PolicyDetails(PolicyType.PersonalOwnership)] PolicyDetails policyDetails)
    {
        policyDetails.OrganizationUserStatus = userStatus;

        var actual = PersonalOwnershipPolicyRequirement.Create([policyDetails]);

        Assert.Equal(shouldBeEnforced, actual.DisablePersonalOwnership);
    }
}
