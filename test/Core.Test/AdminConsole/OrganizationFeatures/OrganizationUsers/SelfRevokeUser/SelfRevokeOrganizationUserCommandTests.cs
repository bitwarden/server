using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.SelfRevokeUser;
using Bit.Core.AdminConsole.Repositories;
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
    public async Task SelfRevokeUser_Success(
        OrganizationUserType userType,
        Guid organizationId,
        Guid userId,
        [OrganizationUser(OrganizationUserStatusType.Confirmed)] OrganizationUser organizationUser,
        Policy policy,
        SutProvider<SelfRevokeOrganizationUserCommand> sutProvider)
    {
        // Arrange
        organizationUser.Type = userType;
        organizationUser.OrganizationId = organizationId;
        organizationUser.UserId = userId;
        policy.Type = PolicyType.OrganizationDataOwnership;
        policy.Enabled = true;
        policy.OrganizationId = organizationId;

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organizationId, PolicyType.OrganizationDataOwnership)
            .Returns(policy);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organizationId, userId)
            .Returns(organizationUser);

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
    public async Task SelfRevokeUser_WhenPolicyDisabled_ThrowsBadRequest(
        Guid organizationId,
        Guid userId,
        Policy policy,
        SutProvider<SelfRevokeOrganizationUserCommand> sutProvider)
    {
        // Arrange
        policy.Type = PolicyType.OrganizationDataOwnership;
        policy.Enabled = false;
        policy.OrganizationId = organizationId;

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organizationId, PolicyType.OrganizationDataOwnership)
            .Returns(policy);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SelfRevokeUserAsync(organizationId, userId));

        Assert.Contains("policy is not enabled", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task SelfRevokeUser_WhenPolicyNotFound_ThrowsBadRequest(
        Guid organizationId,
        Guid userId,
        SutProvider<SelfRevokeOrganizationUserCommand> sutProvider)
    {
        // Arrange
        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organizationId, PolicyType.OrganizationDataOwnership)
            .Returns((Policy)null);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SelfRevokeUserAsync(organizationId, userId));

        Assert.Contains("policy is not enabled", exception.Message);
    }

    [Theory]
    [BitAutoData(OrganizationUserType.Owner)]
    [BitAutoData(OrganizationUserType.Admin)]
    public async Task SelfRevokeUser_WhenUserIsOwnerOrAdmin_ThrowsBadRequest(
        OrganizationUserType userType,
        Guid organizationId,
        Guid userId,
        [OrganizationUser(OrganizationUserStatusType.Confirmed)] OrganizationUser organizationUser,
        Policy policy,
        SutProvider<SelfRevokeOrganizationUserCommand> sutProvider)
    {
        // Arrange
        organizationUser.Type = userType;
        organizationUser.OrganizationId = organizationId;
        organizationUser.UserId = userId;
        policy.Type = PolicyType.OrganizationDataOwnership;
        policy.Enabled = true;
        policy.OrganizationId = organizationId;

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organizationId, PolicyType.OrganizationDataOwnership)
            .Returns(policy);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organizationId, userId)
            .Returns(organizationUser);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SelfRevokeUserAsync(organizationId, userId));

        Assert.Contains("exempt from the organization data ownership policy", exception.Message);
    }

    [Theory]
    [BitAutoData(OrganizationUserStatusType.Invited)]
    [BitAutoData(OrganizationUserStatusType.Accepted)]
    public async Task SelfRevokeUser_WhenUserNotConfirmed_ThrowsBadRequest(
        OrganizationUserStatusType status,
        Guid organizationId,
        Guid userId,
        OrganizationUser organizationUser,
        Policy policy,
        SutProvider<SelfRevokeOrganizationUserCommand> sutProvider)
    {
        // Arrange
        organizationUser.Status = status;
        organizationUser.Type = OrganizationUserType.User;
        organizationUser.OrganizationId = organizationId;
        organizationUser.UserId = userId;
        policy.Type = PolicyType.OrganizationDataOwnership;
        policy.Enabled = true;
        policy.OrganizationId = organizationId;

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organizationId, PolicyType.OrganizationDataOwnership)
            .Returns(policy);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organizationId, userId)
            .Returns(organizationUser);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.SelfRevokeUserAsync(organizationId, userId));

        Assert.Contains("User must be confirmed to self-revoke", exception.Message);
    }

    [Theory, BitAutoData]
    public async Task SelfRevokeUser_WhenUserNotFound_ThrowsNotFoundException(
        Guid organizationId,
        Guid userId,
        Policy policy,
        SutProvider<SelfRevokeOrganizationUserCommand> sutProvider)
    {
        // Arrange
        policy.Type = PolicyType.OrganizationDataOwnership;
        policy.Enabled = true;
        policy.OrganizationId = organizationId;

        sutProvider.GetDependency<IPolicyRepository>()
            .GetByOrganizationIdTypeAsync(organizationId, PolicyType.OrganizationDataOwnership)
            .Returns(policy);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetByOrganizationAsync(organizationId, userId)
            .Returns((OrganizationUser)null);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.SelfRevokeUserAsync(organizationId, userId));
    }
}
