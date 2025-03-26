using AutoFixture.Xunit2;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Enums;
using Bit.Core.Test.AdminConsole.AutoFixture;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public class BasePolicyRequirementFactoryTests
{
    [Theory, AutoData]
    public void ExemptRoles_DoesNotEnforceAgainstThoseRoles(
        [PolicyDetails(PolicyType.SingleOrg, OrganizationUserType.Owner)] PolicyDetails ownerPolicy,
        [PolicyDetails(PolicyType.SingleOrg, OrganizationUserType.Admin)] PolicyDetails adminPolicy,
        [PolicyDetails(PolicyType.SingleOrg, OrganizationUserType.Custom)] PolicyDetails customPolicy,
        [PolicyDetails(PolicyType.SingleOrg)] PolicyDetails userPolicy)
    {
        var sut = new TestPolicyRequirementFactory(
            // These exempt roles are intentionally unusual to make sure we're properly testing the sut
            [OrganizationUserType.User, OrganizationUserType.Custom],
            [],
            false);

        Assert.True(sut.Enforce(ownerPolicy));
        Assert.True(sut.Enforce(adminPolicy));
        Assert.False(sut.Enforce(customPolicy));
        Assert.False(sut.Enforce(userPolicy));
    }

    [Theory, AutoData]
    public void ExemptStatuses_DoesNotEnforceAgainstThoseStatuses(
        [PolicyDetails(PolicyType.SingleOrg, userStatus: OrganizationUserStatusType.Invited)] PolicyDetails invitedPolicy,
        [PolicyDetails(PolicyType.SingleOrg, userStatus: OrganizationUserStatusType.Accepted)] PolicyDetails acceptedPolicy,
        [PolicyDetails(PolicyType.SingleOrg, userStatus: OrganizationUserStatusType.Confirmed)] PolicyDetails confirmedPolicy,
        [PolicyDetails(PolicyType.SingleOrg, userStatus: OrganizationUserStatusType.Revoked)] PolicyDetails revokedPolicy)
    {
        var sut = new TestPolicyRequirementFactory(
            [],
            // These exempt statuses are intentionally unusual to make sure we're properly testing the sut
            [OrganizationUserStatusType.Confirmed, OrganizationUserStatusType.Accepted],
            false);

        Assert.True(sut.Enforce(invitedPolicy));
        Assert.True(sut.Enforce(revokedPolicy));
        Assert.False(sut.Enforce(confirmedPolicy));
        Assert.False(sut.Enforce(acceptedPolicy));
    }

    [Theory, AutoData]
    public void ExemptProviders_DoesNotEnforceAgainstProviders(
        [PolicyDetails(PolicyType.SingleOrg, isProvider: true)] PolicyDetails policy)
    {
        var sut = new TestPolicyRequirementFactory(
            [],
            [],
            true);

        Assert.False(sut.Enforce(policy));
    }

    [Theory, AutoData]
    public void NoExemptions_EnforcesAgainstAdminsAndProviders(
        [PolicyDetails(PolicyType.SingleOrg, OrganizationUserType.Owner, isProvider: true)] PolicyDetails policy)
    {
        var sut = new TestPolicyRequirementFactory(
            [],
            [],
            false);

        Assert.True(sut.Enforce(policy));
    }

    private class TestPolicyRequirementFactory(
        IEnumerable<OrganizationUserType> exemptRoles,
        IEnumerable<OrganizationUserStatusType> exemptStatuses,
        bool exemptProviders
        ) : BasePolicyRequirementFactory<TestPolicyRequirement>
    {
        public override PolicyType PolicyType => PolicyType.SingleOrg;
        protected override IEnumerable<OrganizationUserType> ExemptRoles => exemptRoles;
        protected override IEnumerable<OrganizationUserStatusType> ExemptStatuses => exemptStatuses;

        protected override bool ExemptProviders => exemptProviders;

        public override TestPolicyRequirement Create(IEnumerable<PolicyDetails> policyDetails)
            => new() { Policies = policyDetails };
    }
}
