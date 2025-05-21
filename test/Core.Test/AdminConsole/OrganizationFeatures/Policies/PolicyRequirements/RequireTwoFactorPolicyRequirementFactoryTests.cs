using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.Enums;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

[SutProviderCustomize]
public class RequireTwoFactorPolicyRequirementFactoryTests
{
    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public void CanAcceptInvitation_WithNoPolicies_ReturnsTrue(
        bool twoFactorEnabled, Guid organizationId,
        SutProvider<RequireTwoFactorPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.True(actual.CanAcceptInvitation(twoFactorEnabled, organizationId));
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Revoked)]
    [BitAutoData(OrganizationUserStatusType.Invited)]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    public void CanAcceptInvitation_WithTwoFactorEnabled_ReturnsTrue(
        OrganizationUserStatusType userStatus, Guid organizationId,
        SutProvider<RequireTwoFactorPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(
        [
            new PolicyDetails
            {
                OrganizationId = organizationId,
                PolicyType = PolicyType.TwoFactorAuthentication,
                OrganizationUserStatus = userStatus
            }
        ]);

        Assert.True(actual.CanAcceptInvitation(true, organizationId));
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Revoked)]
    public void CanAcceptInvitation_WithExemptStatus_ReturnsTrue(
        OrganizationUserStatusType userStatus, Guid organizationId,
        SutProvider<RequireTwoFactorPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(
        [
            new PolicyDetails
            {
                OrganizationId = organizationId,
                PolicyType = PolicyType.TwoFactorAuthentication,
                OrganizationUserStatus = userStatus
            }
        ]);

        Assert.True(actual.CanAcceptInvitation(false, organizationId));
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Invited)]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    public void CanAcceptInvitation_WithNonExemptStatus_ReturnsFalse(
        OrganizationUserStatusType userStatus, Guid organizationId,
        SutProvider<RequireTwoFactorPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(
        [
            new PolicyDetails
            {
                OrganizationId = organizationId,
                PolicyType = PolicyType.TwoFactorAuthentication,
                OrganizationUserStatus = userStatus
            }
        ]);

        Assert.False(actual.CanAcceptInvitation(false, organizationId));
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public void CanBeConfirmed_WithNoPolicies_ReturnsTrue(
        bool twoFactorEnabled, Guid organizationId,
        SutProvider<RequireTwoFactorPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.True(actual.CanBeConfirmed(twoFactorEnabled, organizationId));
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    public void CanBeConfirmed_WithTwoFactorEnabled_ReturnsTrue(
        OrganizationUserStatusType userStatus, Guid organizationId,
        SutProvider<RequireTwoFactorPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(
        [
            new PolicyDetails
            {
                OrganizationId = organizationId,
                PolicyType = PolicyType.TwoFactorAuthentication,
                OrganizationUserStatus = userStatus
            }
        ]);

        Assert.True(actual.CanBeConfirmed(true, organizationId));
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Revoked)]
    [BitAutoData(OrganizationUserStatusType.Invited)]
    public void CanBeConfirmed_WithExemptStatus_ReturnsTrue(
        OrganizationUserStatusType userStatus, Guid organizationId,
        SutProvider<RequireTwoFactorPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(
        [
            new PolicyDetails
            {
                OrganizationId = organizationId,
                PolicyType = PolicyType.TwoFactorAuthentication,
                OrganizationUserStatus = userStatus
            }
        ]);

        Assert.True(actual.CanBeConfirmed(false, organizationId));
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    public void CanBeConfirmed_WithNonExemptStatus_ReturnsFalse(
        OrganizationUserStatusType userStatus, Guid organizationId,
        SutProvider<RequireTwoFactorPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(
        [
            new PolicyDetails
            {
                OrganizationId = organizationId,
                PolicyType = PolicyType.TwoFactorAuthentication,
                OrganizationUserStatus = userStatus
            }
        ]);

        Assert.False(actual.CanBeConfirmed(false, organizationId));
    }

    [Theory]
    [BitAutoData(true)]
    [BitAutoData(false)]
    public void CanBeRestored_WithNoPolicies_ReturnsTrue(
        bool twoFactorEnabled, Guid organizationId,
        SutProvider<RequireTwoFactorPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.True(actual.CanBeRestored(twoFactorEnabled, organizationId));
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Revoked)]
    [BitAutoData(OrganizationUserStatusType.Invited)]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    public void CanBeRestored_WithTwoFactorEnabled_ReturnsTrue(
        OrganizationUserStatusType userStatus, Guid organizationId,
        SutProvider<RequireTwoFactorPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(
        [
            new PolicyDetails
            {
                OrganizationId = organizationId,
                PolicyType = PolicyType.TwoFactorAuthentication,
                OrganizationUserStatus = userStatus
            }
        ]);

        Assert.True(actual.CanBeRestored(true, organizationId));
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Revoked)]
    [BitAutoData(OrganizationUserStatusType.Invited)]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    public void CanBeRestored_WithAnyStatus_ReturnsFalse(
        OrganizationUserStatusType userStatus, Guid organizationId,
        SutProvider<RequireTwoFactorPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create(
        [
            new PolicyDetails
            {
                OrganizationId = organizationId,
                PolicyType = PolicyType.TwoFactorAuthentication,
                OrganizationUserStatus = userStatus
            }
        ]);

        Assert.False(actual.CanBeRestored(false, organizationId));
    }

    [Theory, BitAutoData]
    public void TwoFactorPoliciesForActiveMemberships_WithNoPolicies_ReturnsEmptyCollection(
        SutProvider<RequireTwoFactorPolicyRequirementFactory> sutProvider)
    {
        var actual = sutProvider.Sut.Create([]);

        Assert.Empty(actual.TwoFactorPoliciesForActiveMemberships);
    }

    [Theory, BitAutoData]
    public void TwoFactorPoliciesForActiveMemberships_WithMultiplePolicies_ReturnsActiveMemberships(
        Guid orgId1, Guid orgId2, Guid orgId3, Guid orgId4,
        SutProvider<RequireTwoFactorPolicyRequirementFactory> sutProvider)
    {
        var policies = new[]
        {
            new PolicyDetails
            {
                OrganizationId = orgId1,
                PolicyType = PolicyType.TwoFactorAuthentication,
                OrganizationUserStatus = OrganizationUserStatusType.Accepted
            },
            new PolicyDetails
            {
                OrganizationId = orgId2,
                PolicyType = PolicyType.TwoFactorAuthentication,
                OrganizationUserStatus = OrganizationUserStatusType.Confirmed
            },
            new PolicyDetails
            {
                OrganizationId = orgId3,
                PolicyType = PolicyType.TwoFactorAuthentication,
                OrganizationUserStatus = OrganizationUserStatusType.Invited
            },
            new PolicyDetails
            {
                OrganizationId = orgId4,
                PolicyType = PolicyType.TwoFactorAuthentication,
                OrganizationUserStatus = OrganizationUserStatusType.Revoked
            }
        };

        var actual = sutProvider.Sut.Create(policies);

        var result = actual.TwoFactorPoliciesForActiveMemberships.ToList();
        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.OrganizationId == orgId1);
        Assert.Contains(result, p => p.OrganizationId == orgId2);
        Assert.DoesNotContain(result, p => p.OrganizationId == orgId3);
        Assert.DoesNotContain(result, p => p.OrganizationId == orgId4);
    }
}
