using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.RevokeUser.v2;

[SutProviderCustomize]
public class RevokeOrganizationUserCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task RevokeUsersAsync_WithValidUsers_RevokesUsersAndLogsEvents(
        SutProvider<RevokeOrganizationUserCommand> sutProvider,
        Guid organizationId,
        Guid actingUserId,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser2)
    {
        // Arrange
        orgUser1.OrganizationId = orgUser2.OrganizationId = organizationId;
        orgUser1.UserId = Guid.NewGuid();
        orgUser2.UserId = Guid.NewGuid();

        var actingUser = CreateActingUser(actingUserId, false, null);
        var request = new RevokeOrganizationUsersRequest
        {
            OrganizationId = organizationId,
            OrganizationUserIdsToRevoke = [orgUser1.Id, orgUser2.Id],
            PerformedBy = actingUser
        };

        SetupRepositoryMocks(sutProvider, organizationId, organization, [orgUser1, orgUser2]);
        SetupValidatorMock(sutProvider, [
            ValidationResultHelpers.Valid(orgUser1),
            ValidationResultHelpers.Valid(orgUser2)
        ]);

        // Act
        var results = (await sutProvider.Sut.RevokeUsersAsync(request)).ToList();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.Result.IsSuccess));

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .RevokeManyByIdAsync(Arg.Is<IEnumerable<Guid>>(ids =>
                ids.Contains(orgUser1.Id) && ids.Contains(orgUser2.Id)));

        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventsAsync(Arg.Is<IEnumerable<(OrganizationUser, EventType, DateTime?)>>(
                events => events.Count() == 2));

        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncOrgKeysAsync(orgUser1.UserId!.Value);

        await sutProvider.GetDependency<IPushNotificationService>()
            .Received(1)
            .PushSyncOrgKeysAsync(orgUser2.UserId!.Value);
    }

    [Theory]
    [BitAutoData]
    public async Task RevokeUsersAsync_WithSystemUser_LogsEventsWithSystemUserType(
        SutProvider<RevokeOrganizationUserCommand> sutProvider,
        Guid organizationId,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        // Arrange
        orgUser.OrganizationId = organizationId;
        orgUser.UserId = Guid.NewGuid();

        var actingUser = CreateActingUser(null, false, EventSystemUser.SCIM);
        var request = new RevokeOrganizationUsersRequest
        {
            OrganizationId = organizationId,
            OrganizationUserIdsToRevoke = [orgUser.Id],
            PerformedBy = actingUser
        };

        SetupRepositoryMocks(sutProvider, organizationId, organization, [orgUser]);
        SetupValidatorMock(sutProvider, [ValidationResultHelpers.Valid(orgUser)]);

        // Act
        await sutProvider.Sut.RevokeUsersAsync(request);

        // Assert
        await sutProvider.GetDependency<IEventService>()
            .Received(1)
            .LogOrganizationUserEventsAsync(Arg.Is<IEnumerable<(OrganizationUser, EventType, EventSystemUser, DateTime?)>>(
                events => events.All(e => e.Item3 == EventSystemUser.SCIM)));
    }

    [Theory]
    [BitAutoData]
    public async Task RevokeUsersAsync_WithValidationErrors_ReturnsErrorResults(
        SutProvider<RevokeOrganizationUserCommand> sutProvider,
        Guid organizationId,
        Guid actingUserId,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Revoked, OrganizationUserType.User)] OrganizationUser orgUser1,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser2)
    {
        // Arrange
        orgUser1.OrganizationId = orgUser2.OrganizationId = organizationId;

        var actingUser = CreateActingUser(actingUserId, false, null);
        var request = new RevokeOrganizationUsersRequest
        {
            OrganizationId = organizationId,
            OrganizationUserIdsToRevoke = [orgUser1.Id, orgUser2.Id],
            PerformedBy = actingUser
        };

        SetupRepositoryMocks(sutProvider, organizationId, organization, [orgUser1, orgUser2]);
        SetupValidatorMock(sutProvider, [
            ValidationResultHelpers.Invalid(orgUser1, new UserAlreadyRevoked()),
            ValidationResultHelpers.Valid(orgUser2)
        ]);

        // Act
        var results = (await sutProvider.Sut.RevokeUsersAsync(request)).ToList();

        // Assert
        Assert.Equal(2, results.Count);
        var result1 = results.Single(r => r.Id == orgUser1.Id);
        var result2 = results.Single(r => r.Id == orgUser2.Id);

        Assert.True(result1.Result.IsError);
        Assert.True(result2.Result.IsSuccess);

        // Only the valid user should be revoked
        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .RevokeManyByIdAsync(Arg.Is<IEnumerable<Guid>>(ids =>
                ids.Count() == 1 && ids.Contains(orgUser2.Id)));
    }

    [Theory]
    [BitAutoData]
    public async Task RevokeUsersAsync_WhenPushNotificationFails_ContinuesProcessing(
        SutProvider<RevokeOrganizationUserCommand> sutProvider,
        Guid organizationId,
        Guid actingUserId,
        Organization organization,
        [OrganizationUser(OrganizationUserStatusType.Confirmed, OrganizationUserType.User)] OrganizationUser orgUser)
    {
        // Arrange
        orgUser.OrganizationId = organizationId;
        orgUser.UserId = Guid.NewGuid();

        var actingUser = CreateActingUser(actingUserId, false, null);
        var request = new RevokeOrganizationUsersRequest
        {
            OrganizationId = organizationId,
            OrganizationUserIdsToRevoke = [orgUser.Id],
            PerformedBy = actingUser
        };

        SetupRepositoryMocks(sutProvider, organizationId, organization, [orgUser]);
        SetupValidatorMock(sutProvider, [ValidationResultHelpers.Valid(orgUser)]);

        sutProvider.GetDependency<IPushNotificationService>()
            .PushSyncOrgKeysAsync(orgUser.UserId!.Value)
            .Returns(Task.FromException(new Exception("Push notification failed")));

        // Act
        var results = (await sutProvider.Sut.RevokeUsersAsync(request)).ToList();

        // Assert
        Assert.Single(results);
        Assert.True(results[0].Result.IsSuccess);

        // Should log warning but continue
        sutProvider.GetDependency<ILogger<RevokeOrganizationUserCommand>>()
            .Received()
            .Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Any<object>(),
                Arg.Any<Exception>(),
                Arg.Any<Func<object, Exception?, string>>());
    }

    private static IActingUser CreateActingUser(Guid? userId, bool isOwnerOrProvider, EventSystemUser? systemUserType) =>
        (userId, systemUserType) switch
        {
            ({ } id, _) => new StandardUser(id, isOwnerOrProvider),
            (null, { } type) => new SystemUser(type)
        };

    private static void SetupRepositoryMocks(
        SutProvider<RevokeOrganizationUserCommand> sutProvider,
        Guid organizationId,
        Organization organization,
        ICollection<OrganizationUser> organizationUsers)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(organizationUsers);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns(organization);
    }

    private static void SetupValidatorMock(
        SutProvider<RevokeOrganizationUserCommand> sutProvider,
        ICollection<ValidationResult<OrganizationUser>> validationResults)
    {
        sutProvider.GetDependency<IRevokeOrganizationUserValidator>()
            .Validate(Arg.Any<RevokeOrganizationUsersValidationRequest>())
            .Returns(validationResults);
    }
}
