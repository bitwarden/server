using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.SelfRevokeUser;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.SelfRevokeUser;

[SutProviderCustomize]
public class SelfRevokeOrganizationUserCommandTests
{
    [Theory]
    [BitAutoData(OrganizationUserType.User)]
    [BitAutoData(OrganizationUserType.Custom)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task SelfRevokeUser_Success(
        OrganizationUserType userType,
        Guid organizationId,
        Guid userId,
        [OrganizationUser(OrganizationUserStatusType.Confirmed)] OrganizationUser organizationUser,
        SutProvider<SelfRevokeOrganizationUserCommand> sutProvider)
    {
        // Arrange
        organizationUser.Type = userType;
        organizationUser.OrganizationId = organizationId;
        organizationUser.UserId = userId;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organizationId, userId)
            .Returns(organizationUser);

        // Create policy requirement with confirmed user
        var policyDetails = new List<PolicyDetails>
        {
            new()
            {
                OrganizationId = organizationId,
                OrganizationUserId = organizationUser.Id,
                OrganizationUserStatus = OrganizationUserStatusType.Confirmed,
                OrganizationUserType = userType,
                PolicyType = PolicyType.OrganizationDataOwnership
            }
        };
        var policyRequirement = new OrganizationDataOwnershipPolicyRequirement(
            OrganizationDataOwnershipState.Enabled,
            policyDetails);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<OrganizationDataOwnershipPolicyRequirement>(userId)
            .Returns(policyRequirement);

        // Act
        await sutProvider.Sut.SelfRevokeUserAsync(organizationId, userId);

        // Assert
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .RevokeAsync(organizationUser.Id);

        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_SelfRevoked);

        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncOrgKeysAsync(userId);
    }

    [Theory, BitAutoData]
    public async Task SelfRevokeUser_WhenUserNotFound_ThrowsNotFoundException(
        Guid organizationId,
        Guid userId,
        SutProvider<SelfRevokeOrganizationUserCommand> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organizationId, userId)
            .Returns((OrganizationUser)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.SelfRevokeUserAsync(organizationId, userId));
    }

    [Theory, BitAutoData]
    public async Task SelfRevokeUser_WhenNotEligible_ThrowsBadRequest(
        Guid organizationId,
        Guid userId,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser organizationUser,
        SutProvider<SelfRevokeOrganizationUserCommand> sutProvider)
    {
        // Arrange
        organizationUser.OrganizationId = organizationId;
        organizationUser.UserId = userId;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organizationId, userId)
            .Returns(organizationUser);

        // Policy requirement with no policies (disabled)
        var policyRequirement = new OrganizationDataOwnershipPolicyRequirement(
            OrganizationDataOwnershipState.Disabled,
            Enumerable.Empty<PolicyDetails>());

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<OrganizationDataOwnershipPolicyRequirement>(userId)
            .Returns(policyRequirement);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SelfRevokeUserAsync(organizationId, userId));

        Assert.Contains("not eligible for self-revocation", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task SelfRevokeUser_WhenLastOwner_ThrowsBadRequest(
        Guid organizationId,
        Guid userId,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser organizationUser,
        SutProvider<SelfRevokeOrganizationUserCommand> sutProvider)
    {
        // Arrange
        organizationUser.OrganizationId = organizationId;
        organizationUser.UserId = userId;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organizationId, userId)
            .Returns(organizationUser);

        // Create policy requirement with confirmed owner
        var policyDetails = new List<PolicyDetails>
        {
            new()
            {
                OrganizationId = organizationId,
                OrganizationUserId = organizationUser.Id,
                OrganizationUserStatus = OrganizationUserStatusType.Confirmed,
                OrganizationUserType = OrganizationUserType.Owner,
                PolicyType = PolicyType.OrganizationDataOwnership
            }
        };
        var policyRequirement = new OrganizationDataOwnershipPolicyRequirement(
            OrganizationDataOwnershipState.Enabled,
            policyDetails);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<OrganizationDataOwnershipPolicyRequirement>(userId)
            .Returns(policyRequirement);

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>(), true)
            .Returns(false);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SelfRevokeUserAsync(organizationId, userId));

        Assert.Contains("last owner cannot revoke themselves", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task SelfRevokeUser_WhenOwnerButNotLastOwner_Success(
        Guid organizationId,
        Guid userId,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.Owner)] OrganizationUser organizationUser,
        SutProvider<SelfRevokeOrganizationUserCommand> sutProvider)
    {
        // Arrange
        organizationUser.OrganizationId = organizationId;
        organizationUser.UserId = userId;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organizationId, userId)
            .Returns(organizationUser);

        // Create policy requirement with confirmed owner
        var policyDetails = new List<PolicyDetails>
        {
            new()
            {
                OrganizationId = organizationId,
                OrganizationUserId = organizationUser.Id,
                OrganizationUserStatus = OrganizationUserStatusType.Confirmed,
                OrganizationUserType = OrganizationUserType.Owner,
                PolicyType = PolicyType.OrganizationDataOwnership
            }
        };
        var policyRequirement = new OrganizationDataOwnershipPolicyRequirement(
            OrganizationDataOwnershipState.Enabled,
            policyDetails);

        sutProvider.GetDependency<IPolicyRequirementQuery>()
            .GetAsync<OrganizationDataOwnershipPolicyRequirement>(userId)
            .Returns(policyRequirement);

        sutProvider.GetDependency<IHasConfirmedOwnersExceptQuery>()
            .HasConfirmedOwnersExceptAsync(organizationId, Arg.Any<IEnumerable<Guid>>(), true)
            .Returns(true);

        // Act
        await sutProvider.Sut.SelfRevokeUserAsync(organizationId, userId);

        // Assert
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .RevokeAsync(organizationUser.Id);

        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventAsync(organizationUser, EventType.OrganizationUser_SelfRevoked);
    }
}
