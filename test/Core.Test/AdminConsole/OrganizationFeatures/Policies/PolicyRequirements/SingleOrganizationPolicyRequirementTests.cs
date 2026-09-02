using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements.Errors;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

public class SingleOrganizationPolicyRequirementTests
{
    [Fact]
    public void CanCreateOrganization_WithNoPolicies_ReturnsNoError()
    {
        var sut = new SingleOrganizationPolicyRequirement([]);

        var result = sut.CanCreateOrganization();

        Assert.Null(result);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    public void CanCreateOrganization_WithAcceptedOrConfirmedUser_ReturnsError(
        OrganizationUserStatusType status, Guid orgId)
    {
        var sut = new SingleOrganizationPolicyRequirement(
        [
            new PolicyDetails
            {
                OrganizationId = orgId,
                OrganizationUserStatus = status,
                PolicyType = PolicyType.SingleOrg
            }
        ]);

        var result = sut.CanCreateOrganization();

        Assert.NotNull(result);
        Assert.IsType<UserCannotCreateOrg>(result);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Invited)]
    [BitAutoData(OrganizationUserStatusType.Revoked)]
    public void CanCreateOrganization_WithInvitedOrRevokedUser_ReturnsNoError(
        OrganizationUserStatusType status, Guid orgId)
    {
        var sut = new SingleOrganizationPolicyRequirement(
        [
            new PolicyDetails
            {
                OrganizationId = orgId,
                OrganizationUserStatus = status,
                PolicyType = PolicyType.SingleOrg
            }
        ]);

        var result = sut.CanCreateOrganization();

        Assert.Null(result);
    }

    [Theory]
    [BitAutoData]
    public void CanJoinOrganization_NoPolicyDetails_NoOtherOrgs_ReturnsNoError(Guid targetOrgId, Guid userId)
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
        Assert.IsType<UserIsAMemberOfAnotherOrganization>(result);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    [BitAutoData(OrganizationUserStatusType.Confirmed)]
    public void CanJoinOrganization_OtherOrgHasPolicy_ReturnsOtherOrgError(OrganizationUserStatusType status,
        Guid targetOrgId, Guid otherOrgId, Guid userId)
    {
        var sut = new SingleOrganizationPolicyRequirement(
        [
            new PolicyDetails
            {
                OrganizationId = otherOrgId,
                OrganizationUserStatus = status,
                PolicyType = PolicyType.SingleOrg
            }
        ]);

        var allOrgUsers = new List<OrganizationUser>
        {
            new() { UserId = userId, OrganizationId = targetOrgId }
        };

        var result = sut.CanJoinOrganization(targetOrgId, allOrgUsers);

        Assert.NotNull(result);
        Assert.IsType<UserIsAMemberOfAnOrganizationThatHasSingleOrgPolicy>(result);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Invited)]
    [BitAutoData(OrganizationUserStatusType.Revoked)]
    public void CanJoinOrganization_OtherOrgHasPolicy_WithInvitedOrRevokedUser_ReturnsNull(
        OrganizationUserStatusType status, Guid targetOrgId, Guid otherOrgId, Guid userId)
    {
        var sut = new SingleOrganizationPolicyRequirement(
        [
            new PolicyDetails
            {
                OrganizationId = otherOrgId,
                OrganizationUserStatus = status,
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
    public void CanJoinOrganization_EmptyOrgUsers_NoPolicies_ReturnsNull(Guid targetOrgId)
    {
        var sut = new SingleOrganizationPolicyRequirement([]);

        var result = sut.CanJoinOrganization(targetOrgId, new List<OrganizationUser>());

        Assert.Null(result);
    }
}
