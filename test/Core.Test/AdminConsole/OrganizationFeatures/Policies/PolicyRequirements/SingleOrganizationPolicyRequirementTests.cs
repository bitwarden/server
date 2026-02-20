using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public class SingleOrganizationPolicyRequirementTests
{
    [Fact]
    public void CanCreateOrganization_WithNoPolicies_ReturnsNull()
    {
        var sut = new SingleOrganizationPolicyRequirement([]);

        var result = sut.CanCreateOrganization();

        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public void CanCreateOrganization_WithAcceptedPolicy_ReturnsError(Guid orgId)
    {
        var sut = new SingleOrganizationPolicyRequirement(
        [
            new PolicyDetails
            {
                OrganizationId = orgId,
                OrganizationUserStatus = OrganizationUserStatusType.Accepted,
                PolicyType = PolicyType.SingleOrg
            }
        ]);

        var result = sut.CanCreateOrganization();

        Assert.NotNull(result);
        Assert.IsType<SingleOrganizationPolicyRequirement.UserCannotCreateOrg>(result);
    }

    [Theory]
    [BitAutoData]
    public void CanCreateOrganization_WithConfirmedPolicy_ReturnsError(Guid orgId)
    {
        var sut = new SingleOrganizationPolicyRequirement(
        [
            new PolicyDetails
            {
                OrganizationId = orgId,
                OrganizationUserStatus = OrganizationUserStatusType.Confirmed,
                PolicyType = PolicyType.SingleOrg
            }
        ]);

        var result = sut.CanCreateOrganization();

        Assert.NotNull(result);
        Assert.IsType<SingleOrganizationPolicyRequirement.UserCannotCreateOrg>(result);
    }

    [Theory]
    [BitAutoData]
    public void CanCreateOrganization_WithInvitedPolicy_ReturnsNull(Guid orgId)
    {
        var sut = new SingleOrganizationPolicyRequirement(
        [
            new PolicyDetails
            {
                OrganizationId = orgId,
                OrganizationUserStatus = OrganizationUserStatusType.Invited,
                PolicyType = PolicyType.SingleOrg
            }
        ]);

        var result = sut.CanCreateOrganization();

        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public void CanCreateOrganization_WithRevokedPolicy_ReturnsNull(Guid orgId)
    {
        var sut = new SingleOrganizationPolicyRequirement(
        [
            new PolicyDetails
            {
                OrganizationId = orgId,
                OrganizationUserStatus = OrganizationUserStatusType.Revoked,
                PolicyType = PolicyType.SingleOrg
            }
        ]);

        var result = sut.CanCreateOrganization();

        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public void IsEnabledForTargetOrganization_WithMatchingPolicy_ReturnsTrue(Guid orgId)
    {
        var sut = new SingleOrganizationPolicyRequirement(
        [
            new PolicyDetails { OrganizationId = orgId, PolicyType = PolicyType.SingleOrg }
        ]);

        Assert.True(sut.IsEnabledForTargetOrganization(orgId));
    }

    [Theory]
    [BitAutoData]
    public void IsEnabledForTargetOrganization_WithNonMatchingPolicy_ReturnsFalse(Guid orgId)
    {
        var sut = new SingleOrganizationPolicyRequirement(
        [
            new PolicyDetails { OrganizationId = Guid.NewGuid(), PolicyType = PolicyType.SingleOrg }
        ]);

        Assert.False(sut.IsEnabledForTargetOrganization(orgId));
    }

    [Theory]
    [BitAutoData]
    public void IsEnabledForTargetOrganization_WithNoPolicies_ReturnsFalse(Guid orgId)
    {
        var sut = new SingleOrganizationPolicyRequirement([]);

        Assert.False(sut.IsEnabledForTargetOrganization(orgId));
    }

    [Theory]
    [BitAutoData]
    public void IsCompliantWithTargetOrganization_TargetHasPolicy_UserOnlyInTargetOrg_ReturnsNull(
        Guid targetOrgId, Guid userId)
    {
        var sut = new SingleOrganizationPolicyRequirement(
        [
            new PolicyDetails
            {
                OrganizationId = targetOrgId,
                OrganizationUserStatus = OrganizationUserStatusType.Accepted,
                PolicyType = PolicyType.SingleOrg
            }
        ]);

        var allOrgUsers = new List<OrganizationUser>
        {
            new() { UserId = userId, OrganizationId = targetOrgId }
        };

        var result = sut.IsCompliantWithTargetOrganization(targetOrgId, allOrgUsers);

        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public void IsCompliantWithTargetOrganization_TargetHasPolicy_UserInOtherOrgs_ReturnsError(
        Guid targetOrgId, Guid otherOrgId, Guid userId)
    {
        var sut = new SingleOrganizationPolicyRequirement(
        [
            new PolicyDetails
            {
                OrganizationId = targetOrgId,
                OrganizationUserStatus = OrganizationUserStatusType.Accepted,
                PolicyType = PolicyType.SingleOrg
            }
        ]);

        var allOrgUsers = new List<OrganizationUser>
        {
            new() { UserId = userId, OrganizationId = targetOrgId },
            new() { UserId = userId, OrganizationId = otherOrgId }
        };

        var result = sut.IsCompliantWithTargetOrganization(targetOrgId, allOrgUsers);

        Assert.NotNull(result);
        Assert.IsType<SingleOrganizationPolicyRequirement.UserIsAMemberOfAnotherOrganization>(result);
    }

    [Theory]
    [BitAutoData]
    public void IsCompliantWithTargetOrganization_TargetDoesNotHavePolicy_ReturnsNull(
        Guid targetOrgId, Guid otherOrgId, Guid userId)
    {
        // Policy is for a different org, not the target
        var sut = new SingleOrganizationPolicyRequirement(
        [
            new PolicyDetails
            {
                OrganizationId = otherOrgId,
                OrganizationUserStatus = OrganizationUserStatusType.Confirmed,
                PolicyType = PolicyType.SingleOrg
            }
        ]);

        var allOrgUsers = new List<OrganizationUser>
        {
            new() { UserId = userId, OrganizationId = targetOrgId },
            new() { UserId = userId, OrganizationId = otherOrgId }
        };

        var result = sut.IsCompliantWithTargetOrganization(targetOrgId, allOrgUsers);

        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public void IsCompliantWithTargetOrganization_NoPolicies_ReturnsNull(
        Guid targetOrgId, Guid userId)
    {
        var sut = new SingleOrganizationPolicyRequirement([]);

        var allOrgUsers = new List<OrganizationUser>
        {
            new() { UserId = userId, OrganizationId = targetOrgId }
        };

        var result = sut.IsCompliantWithTargetOrganization(targetOrgId, allOrgUsers);

        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public void IsCompliantWithTargetOrganization_TargetHasPolicy_EmptyOrgUsers_ReturnsNull(
        Guid targetOrgId)
    {
        var sut = new SingleOrganizationPolicyRequirement(
        [
            new PolicyDetails
            {
                OrganizationId = targetOrgId,
                OrganizationUserStatus = OrganizationUserStatusType.Accepted,
                PolicyType = PolicyType.SingleOrg
            }
        ]);

        var result = sut.IsCompliantWithTargetOrganization(targetOrgId, new List<OrganizationUser>());

        // Empty org users means user is not in any other org, so compliant
        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public void IsEnabledForOtherOrganizationsUserIsAPartOf_OtherOrgHasAcceptedPolicy_ReturnsError(
        Guid targetOrgId, Guid otherOrgId)
    {
        var sut = new SingleOrganizationPolicyRequirement(
        [
            new PolicyDetails
            {
                OrganizationId = otherOrgId,
                OrganizationUserStatus = OrganizationUserStatusType.Accepted,
                PolicyType = PolicyType.SingleOrg
            }
        ]);

        var result = sut.IsEnabledForOtherOrganizationsUserIsAPartOf(targetOrgId);

        Assert.NotNull(result);
        Assert.IsType<SingleOrganizationPolicyRequirement.UserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy>(result);
    }

    [Theory]
    [BitAutoData]
    public void IsEnabledForOtherOrganizationsUserIsAPartOf_OtherOrgHasConfirmedPolicy_ReturnsError(
        Guid targetOrgId, Guid otherOrgId)
    {
        var sut = new SingleOrganizationPolicyRequirement(
        [
            new PolicyDetails
            {
                OrganizationId = otherOrgId,
                OrganizationUserStatus = OrganizationUserStatusType.Confirmed,
                PolicyType = PolicyType.SingleOrg
            }
        ]);

        var result = sut.IsEnabledForOtherOrganizationsUserIsAPartOf(targetOrgId);

        Assert.NotNull(result);
        Assert.IsType<SingleOrganizationPolicyRequirement.UserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy>(result);
    }

    [Theory]
    [BitAutoData]
    public void IsEnabledForOtherOrganizationsUserIsAPartOf_OtherOrgHasInvitedPolicy_ReturnsNull(
        Guid targetOrgId, Guid otherOrgId)
    {
        var sut = new SingleOrganizationPolicyRequirement(
        [
            new PolicyDetails
            {
                OrganizationId = otherOrgId,
                OrganizationUserStatus = OrganizationUserStatusType.Invited,
                PolicyType = PolicyType.SingleOrg
            }
        ]);

        var result = sut.IsEnabledForOtherOrganizationsUserIsAPartOf(targetOrgId);

        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public void IsEnabledForOtherOrganizationsUserIsAPartOf_OtherOrgHasRevokedPolicy_ReturnsNull(
        Guid targetOrgId, Guid otherOrgId)
    {
        var sut = new SingleOrganizationPolicyRequirement(
        [
            new PolicyDetails
            {
                OrganizationId = otherOrgId,
                OrganizationUserStatus = OrganizationUserStatusType.Revoked,
                PolicyType = PolicyType.SingleOrg
            }
        ]);

        var result = sut.IsEnabledForOtherOrganizationsUserIsAPartOf(targetOrgId);

        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public void IsEnabledForOtherOrganizationsUserIsAPartOf_PolicyIsForTargetOrg_ReturnsNull(
        Guid targetOrgId)
    {
        // Only policy is for the target org itself, not another org
        var sut = new SingleOrganizationPolicyRequirement(
        [
            new PolicyDetails
            {
                OrganizationId = targetOrgId,
                OrganizationUserStatus = OrganizationUserStatusType.Confirmed,
                PolicyType = PolicyType.SingleOrg
            }
        ]);

        var result = sut.IsEnabledForOtherOrganizationsUserIsAPartOf(targetOrgId);

        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public void IsEnabledForOtherOrganizationsUserIsAPartOf_NoPolicies_ReturnsNull(Guid targetOrgId)
    {
        var sut = new SingleOrganizationPolicyRequirement([]);

        var result = sut.IsEnabledForOtherOrganizationsUserIsAPartOf(targetOrgId);

        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public void CanJoinOrganization_NoPolicies_NoOtherOrgs_ReturnsNull(
        Guid targetOrgId, Guid userId)
    {
        var sut = new SingleOrganizationPolicyRequirement([]);

        var allOrgUsers = new List<OrganizationUser>
        {
            new() { UserId = userId, OrganizationId = targetOrgId }
        };

        var result = sut.CanJoinOrganization(targetOrgId, allOrgUsers);

        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public void CanJoinOrganization_TargetHasPolicy_UserInOtherOrg_ReturnsTargetOrgError(
        Guid targetOrgId, Guid otherOrgId, Guid userId)
    {
        var sut = new SingleOrganizationPolicyRequirement(
        [
            new PolicyDetails
            {
                OrganizationId = targetOrgId,
                OrganizationUserStatus = OrganizationUserStatusType.Accepted,
                PolicyType = PolicyType.SingleOrg
            }
        ]);

        var allOrgUsers = new List<OrganizationUser>
        {
            new() { UserId = userId, OrganizationId = targetOrgId },
            new() { UserId = userId, OrganizationId = otherOrgId }
        };

        var result = sut.CanJoinOrganization(targetOrgId, allOrgUsers);

        Assert.NotNull(result);
        Assert.IsType<SingleOrganizationPolicyRequirement.UserIsAMemberOfAnotherOrganization>(result);
    }

    [Theory]
    [BitAutoData]
    public void CanJoinOrganization_OtherOrgHasPolicy_ReturnsOtherOrgError(
        Guid targetOrgId, Guid otherOrgId, Guid userId)
    {
        var sut = new SingleOrganizationPolicyRequirement(
        [
            new PolicyDetails
            {
                OrganizationId = otherOrgId,
                OrganizationUserStatus = OrganizationUserStatusType.Confirmed,
                PolicyType = PolicyType.SingleOrg
            }
        ]);

        var allOrgUsers = new List<OrganizationUser>
        {
            new() { UserId = userId, OrganizationId = targetOrgId }
        };

        var result = sut.CanJoinOrganization(targetOrgId, allOrgUsers);

        Assert.NotNull(result);
        Assert.IsType<SingleOrganizationPolicyRequirement.UserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy>(result);
    }

    [Theory]
    [BitAutoData]
    public void CanJoinOrganization_BothTargetAndOtherOrgHavePolicy_UserInOtherOrg_ReturnsTargetOrgError(
        Guid targetOrgId, Guid otherOrgId, Guid userId)
    {
        // When both checks would fail, the target org check takes priority (it's checked first)
        var sut = new SingleOrganizationPolicyRequirement(
        [
            new PolicyDetails
            {
                OrganizationId = targetOrgId,
                OrganizationUserStatus = OrganizationUserStatusType.Accepted,
                PolicyType = PolicyType.SingleOrg
            },
            new PolicyDetails
            {
                OrganizationId = otherOrgId,
                OrganizationUserStatus = OrganizationUserStatusType.Confirmed,
                PolicyType = PolicyType.SingleOrg
            }
        ]);

        var allOrgUsers = new List<OrganizationUser>
        {
            new() { UserId = userId, OrganizationId = targetOrgId },
            new() { UserId = userId, OrganizationId = otherOrgId }
        };

        var result = sut.CanJoinOrganization(targetOrgId, allOrgUsers);

        Assert.NotNull(result);
        // Target org check returns first since it's checked first via ??
        Assert.IsType<SingleOrganizationPolicyRequirement.UserIsAMemberOfAnotherOrganization>(result);
    }

    [Theory]
    [BitAutoData]
    public void CanJoinOrganization_TargetHasPolicy_UserOnlyInTargetOrg_ReturnsNull(
        Guid targetOrgId, Guid userId)
    {
        var sut = new SingleOrganizationPolicyRequirement(
        [
            new PolicyDetails
            {
                OrganizationId = targetOrgId,
                OrganizationUserStatus = OrganizationUserStatusType.Accepted,
                PolicyType = PolicyType.SingleOrg
            }
        ]);

        var allOrgUsers = new List<OrganizationUser>
        {
            new() { UserId = userId, OrganizationId = targetOrgId }
        };

        var result = sut.CanJoinOrganization(targetOrgId, allOrgUsers);

        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public void CanJoinOrganization_OtherOrgHasPolicy_WithInvitedStatus_ReturnsNull(
        Guid targetOrgId, Guid otherOrgId, Guid userId)
    {
        // Invited status should not trigger the "other org forbids" check
        var sut = new SingleOrganizationPolicyRequirement(
        [
            new PolicyDetails
            {
                OrganizationId = otherOrgId,
                OrganizationUserStatus = OrganizationUserStatusType.Invited,
                PolicyType = PolicyType.SingleOrg
            }
        ]);

        var allOrgUsers = new List<OrganizationUser>
        {
            new() { UserId = userId, OrganizationId = targetOrgId }
        };

        var result = sut.CanJoinOrganization(targetOrgId, allOrgUsers);

        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public void CanJoinOrganization_EmptyOrgUsers_NoPolicies_ReturnsNull(Guid targetOrgId)
    {
        var sut = new SingleOrganizationPolicyRequirement([]);

        var result = sut.CanJoinOrganization(targetOrgId, new List<OrganizationUser>());

        Assert.Null(result);
    }
}
