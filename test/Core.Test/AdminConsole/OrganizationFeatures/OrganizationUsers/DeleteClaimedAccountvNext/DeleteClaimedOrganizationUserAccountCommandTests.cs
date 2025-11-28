using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccount;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.Interfaces;
using Bit.Core.AdminConsole.Utilities.v2;
using Bit.Core.AdminConsole.Utilities.v2.Validation;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationUserFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.DeleteClaimedAccountvNext;

[SutProviderCustomize]
public class DeleteClaimedOrganizationUserAccountCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task DeleteUserAsync_WithValidSingleUser_CallsDeleteManyUsersAsync(
        SutProvider<DeleteClaimedOrganizationUserAccountCommand> sutProvider,
        User user,
        Guid organizationId,
        Guid deletingUserId,
        [OrganizationUser] OrganizationUser organizationUser)
    {
        organizationUser.UserId = user.Id;
        organizationUser.OrganizationId = organizationId;

        var request = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = organizationUser.Id,
            OrganizationUser = organizationUser,
            User = user,
            DeletingUserId = deletingUserId,
            IsClaimed = true
        };
        var validationResult = CreateSuccessfulValidationResult(request);

        SetupRepositoryMocks(sutProvider,
            new List<OrganizationUser> { organizationUser },
            [user],
            organizationId,
            new Dictionary<Guid, bool> { { organizationUser.Id, true } });

        SetupValidatorMock(sutProvider, [validationResult]);

        var result = await sutProvider.Sut.DeleteUserAsync(organizationId, organizationUser.Id, deletingUserId);

        Assert.Equal(organizationUser.Id, result.Id);
        Assert.True(result.Result.IsSuccess);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(organizationUser.Id)));

        await AssertSuccessfulUserOperations(sutProvider, [user], [organizationUser]);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteManyUsersAsync_WithEmptyUserIds_ReturnsEmptyResults(
        SutProvider<DeleteClaimedOrganizationUserAccountCommand> sutProvider,
        Guid organizationId,
        Guid deletingUserId)
    {
        var results = await sutProvider.Sut.DeleteManyUsersAsync(organizationId, [], deletingUserId);

        Assert.Empty(results);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteManyUsersAsync_WithValidUsers_DeletesUsersAndLogsEvents(
        SutProvider<DeleteClaimedOrganizationUserAccountCommand> sutProvider,
        User user1,
        User user2,
        Guid organizationId,
        Guid deletingUserId,
        [OrganizationUser] OrganizationUser orgUser1,
        [OrganizationUser] OrganizationUser orgUser2)
    {
        // Arrange
        orgUser1.OrganizationId = orgUser2.OrganizationId = organizationId;
        orgUser1.UserId = user1.Id;
        orgUser2.UserId = user2.Id;

        var request1 = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = orgUser1.Id,
            OrganizationUser = orgUser1,
            User = user1,
            DeletingUserId = deletingUserId,
            IsClaimed = true
        };
        var request2 = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = orgUser2.Id,
            OrganizationUser = orgUser2,
            User = user2,
            DeletingUserId = deletingUserId,
            IsClaimed = true
        };

        var validationResults = new[]
        {
            CreateSuccessfulValidationResult(request1),
            CreateSuccessfulValidationResult(request2)
        };

        SetupRepositoryMocks(sutProvider,
            new List<OrganizationUser> { orgUser1, orgUser2 },
            [user1, user2],
            organizationId,
            new Dictionary<Guid, bool> { { orgUser1.Id, true }, { orgUser2.Id, true } });

        SetupValidatorMock(sutProvider, validationResults);

        var results = await sutProvider.Sut.DeleteManyUsersAsync(organizationId, [orgUser1.Id, orgUser2.Id], deletingUserId);

        var resultsList = results.ToList();
        Assert.Equal(2, resultsList.Count);
        Assert.All(resultsList, result => Assert.True(result.Result.IsSuccess));

        await AssertSuccessfulUserOperations(sutProvider, [user1, user2], [orgUser1, orgUser2]);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteManyUsersAsync_WithValidationErrors_ReturnsErrorResults(
        SutProvider<DeleteClaimedOrganizationUserAccountCommand> sutProvider,
        Guid organizationId,
        Guid orgUserId1,
        Guid orgUserId2,
        Guid deletingUserId)
    {
        // Arrange
        var request1 = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = orgUserId1,
            DeletingUserId = deletingUserId
        };
        var request2 = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = orgUserId2,
            DeletingUserId = deletingUserId
        };

        var validationResults = new[]
        {
            CreateFailedValidationResult(request1, new UserNotClaimedError()),
            CreateFailedValidationResult(request2, new InvalidUserStatusError())
        };

        SetupRepositoryMocks(sutProvider, [], [], organizationId, new Dictionary<Guid, bool>());
        SetupValidatorMock(sutProvider, validationResults);

        var results = await sutProvider.Sut.DeleteManyUsersAsync(organizationId, [orgUserId1, orgUserId2], deletingUserId);

        var resultsList = results.ToList();
        Assert.Equal(2, resultsList.Count);

        Assert.Equal(orgUserId1, resultsList[0].Id);
        Assert.True(resultsList[0].Result.IsError);
        Assert.IsType<UserNotClaimedError>(resultsList[0].Result.AsError);

        Assert.Equal(orgUserId2, resultsList[1].Id);
        Assert.True(resultsList[1].Result.IsError);
        Assert.IsType<InvalidUserStatusError>(resultsList[1].Result.AsError);

        await AssertNoUserOperations(sutProvider);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteManyUsersAsync_WithMixedValidationResults_HandlesPartialSuccessCorrectly(
        SutProvider<DeleteClaimedOrganizationUserAccountCommand> sutProvider,
        User validUser,
        Guid organizationId,
        Guid validOrgUserId,
        Guid invalidOrgUserId,
        Guid deletingUserId,
        [OrganizationUser] OrganizationUser validOrgUser)
    {
        validOrgUser.Id = validOrgUserId;
        validOrgUser.UserId = validUser.Id;
        validOrgUser.OrganizationId = organizationId;

        var validRequest = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = validOrgUserId,
            OrganizationUser = validOrgUser,
            User = validUser,
            DeletingUserId = deletingUserId,
            IsClaimed = true
        };
        var invalidRequest = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = invalidOrgUserId,
            DeletingUserId = deletingUserId
        };

        var validationResults = new[]
        {
            CreateSuccessfulValidationResult(validRequest),
            CreateFailedValidationResult(invalidRequest, new UserNotFoundError())
        };

        SetupRepositoryMocks(sutProvider,
            new List<OrganizationUser> { validOrgUser },
            [validUser],
            organizationId,
            new Dictionary<Guid, bool> { { validOrgUserId, true } });

        SetupValidatorMock(sutProvider, validationResults);

        var results = await sutProvider.Sut.DeleteManyUsersAsync(organizationId, [validOrgUserId, invalidOrgUserId], deletingUserId);

        var resultsList = results.ToList();
        Assert.Equal(2, resultsList.Count);

        var validResult = resultsList.First(r => r.Id == validOrgUserId);
        var invalidResult = resultsList.First(r => r.Id == invalidOrgUserId);

        Assert.True(validResult.Result.IsSuccess);
        Assert.True(invalidResult.Result.IsError);
        Assert.IsType<UserNotFoundError>(invalidResult.Result.AsError);

        await AssertSuccessfulUserOperations(sutProvider, [validUser], [validOrgUser]);
    }

    [Theory]
    [BitAutoData]
    public async Task DeleteManyUsersAsync_CancelPremiumsAsync_HandlesGatewayExceptionAndLogsWarning(
        SutProvider<DeleteClaimedOrganizationUserAccountCommand> sutProvider,
        User user,
        Guid organizationId,
        Guid deletingUserId,
        [OrganizationUser] OrganizationUser orgUser)
    {
        orgUser.UserId = user.Id;
        orgUser.OrganizationId = organizationId;

        var request = new DeleteUserValidationRequest
        {
            OrganizationId = organizationId,
            OrganizationUserId = orgUser.Id,
            OrganizationUser = orgUser,
            User = user,
            DeletingUserId = deletingUserId,
            IsClaimed = true
        };
        var validationResult = CreateSuccessfulValidationResult(request);

        SetupRepositoryMocks(sutProvider,
            new List<OrganizationUser> { orgUser },
            [user],
            organizationId,
            new Dictionary<Guid, bool> { { orgUser.Id, true } });

        SetupValidatorMock(sutProvider, [validationResult]);

        var gatewayException = new GatewayException("Payment gateway error");
        sutProvider.GetDependency<IUserService>()
            .CancelPremiumAsync(user)
            .ThrowsAsync(gatewayException);

        var results = await sutProvider.Sut.DeleteManyUsersAsync(organizationId, [orgUser.Id], deletingUserId);

        var resultsList = results.ToList();
        Assert.Single(resultsList);
        Assert.True(resultsList.First().Result.IsSuccess);

        await sutProvider.GetDependency<IUserService>().Received(1).CancelPremiumAsync(user);
        await AssertSuccessfulUserOperations(sutProvider, [user], [orgUser]);

        sutProvider.GetDependency<ILogger<DeleteClaimedOrganizationUserAccountCommand>>()
            .Received(1)
            .Log(
                LogLevel.Warning,
                Arg.Any<EventId>(),
                Arg.Is<object>(o => o.ToString()!.Contains($"Failed to cancel premium subscription for {user.Id}")),
                gatewayException,
                Arg.Any<Func<object, Exception?, string>>());
    }


    [Theory]
    [BitAutoData]
    public async Task CreateInternalRequests_CreatesCorrectRequestsForAllUsers(
        SutProvider<DeleteClaimedOrganizationUserAccountCommand> sutProvider,
        User user1,
        User user2,
        Guid organizationId,
        Guid deletingUserId,
        [OrganizationUser] OrganizationUser orgUser1,
        [OrganizationUser] OrganizationUser orgUser2)
    {
        orgUser1.UserId = user1.Id;
        orgUser2.UserId = user2.Id;
        var orgUserIds = new[] { orgUser1.Id, orgUser2.Id };
        var orgUsers = new List<OrganizationUser> { orgUser1, orgUser2 };
        var users = new[] { user1, user2 };
        var claimedStatuses = new Dictionary<Guid, bool> { { orgUser1.Id, true }, { orgUser2.Id, false } };

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(orgUsers);

        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(ids => ids.Contains(user1.Id) && ids.Contains(user2.Id)))
            .Returns(users);

        sutProvider.GetDependency<IGetOrganizationUsersClaimedStatusQuery>()
            .GetUsersOrganizationClaimedStatusAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(claimedStatuses);

        sutProvider.GetDependency<IDeleteClaimedOrganizationUserAccountValidator>()
            .ValidateAsync(Arg.Any<IEnumerable<DeleteUserValidationRequest>>())
            .Returns(callInfo =>
            {
                var requests = callInfo.Arg<IEnumerable<DeleteUserValidationRequest>>();
                return requests.Select(r => CreateFailedValidationResult(r, new UserNotFoundError()));
            });

        // Act
        await sutProvider.Sut.DeleteManyUsersAsync(organizationId, orgUserIds, deletingUserId);

        // Assert
        await sutProvider.GetDependency<IDeleteClaimedOrganizationUserAccountValidator>()
            .Received(1)
            .ValidateAsync(Arg.Is<IEnumerable<DeleteUserValidationRequest>>(requests =>
                requests.Count() == 2 &&
                requests.Any(r => r.OrganizationUserId == orgUser1.Id &&
                                  r.OrganizationId == organizationId &&
                                  r.OrganizationUser == orgUser1 &&
                                  r.User == user1 &&
                                  r.DeletingUserId == deletingUserId &&
                                  r.IsClaimed == true) &&
                requests.Any(r => r.OrganizationUserId == orgUser2.Id &&
                                  r.OrganizationId == organizationId &&
                                  r.OrganizationUser == orgUser2 &&
                                  r.User == user2 &&
                                  r.DeletingUserId == deletingUserId &&
                                  r.IsClaimed == false)));
    }

    [Theory]
    [BitAutoData]
    public async Task GetUsersAsync_WithNullUserIds_ReturnsEmptyCollection(
        SutProvider<DeleteClaimedOrganizationUserAccountCommand> sutProvider,
        Guid organizationId,
        Guid deletingUserId,
        [OrganizationUser] OrganizationUser orgUserWithoutUserId)
    {
        orgUserWithoutUserId.UserId = null; // Intentionally setting to null for test case

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(new List<OrganizationUser> { orgUserWithoutUserId });

        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(ids => !ids.Any()))
            .Returns([]);

        sutProvider.GetDependency<IDeleteClaimedOrganizationUserAccountValidator>()
            .ValidateAsync(Arg.Any<IEnumerable<DeleteUserValidationRequest>>())
            .Returns(callInfo =>
            {
                var requests = callInfo.Arg<IEnumerable<DeleteUserValidationRequest>>();
                return requests.Select(r => CreateFailedValidationResult(r, new UserNotFoundError()));
            });

        // Act
        await sutProvider.Sut.DeleteManyUsersAsync(organizationId, [orgUserWithoutUserId.Id], deletingUserId);

        // Assert
        await sutProvider.GetDependency<IDeleteClaimedOrganizationUserAccountValidator>()
            .Received(1)
            .ValidateAsync(Arg.Is<IEnumerable<DeleteUserValidationRequest>>(requests =>
                requests.Count() == 1 &&
                requests.Single().User == null));

        await sutProvider.GetDependency<IUserRepository>().Received(1)
            .GetManyAsync(Arg.Is<IEnumerable<Guid>>(ids => !ids.Any()));
    }

    private static ValidationResult<DeleteUserValidationRequest> CreateSuccessfulValidationResult(
        DeleteUserValidationRequest request) =>
        ValidationResultHelpers.Valid(request);

    private static ValidationResult<DeleteUserValidationRequest> CreateFailedValidationResult(
        DeleteUserValidationRequest request,
        Error error) =>
        ValidationResultHelpers.Invalid(request, error);

    private static void SetupRepositoryMocks(
        SutProvider<DeleteClaimedOrganizationUserAccountCommand> sutProvider,
        ICollection<OrganizationUser> orgUsers,
        IEnumerable<User> users,
        Guid organizationId,
        Dictionary<Guid, bool> claimedStatuses)
    {
        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(orgUsers);

        sutProvider.GetDependency<IUserRepository>()
            .GetManyAsync(Arg.Any<IEnumerable<Guid>>())
            .Returns(users);

        sutProvider.GetDependency<IGetOrganizationUsersClaimedStatusQuery>()
            .GetUsersOrganizationClaimedStatusAsync(organizationId, Arg.Any<IEnumerable<Guid>>())
            .Returns(claimedStatuses);
    }

    private static void SetupValidatorMock(
        SutProvider<DeleteClaimedOrganizationUserAccountCommand> sutProvider,
        IEnumerable<ValidationResult<DeleteUserValidationRequest>> validationResults)
    {
        sutProvider.GetDependency<IDeleteClaimedOrganizationUserAccountValidator>()
            .ValidateAsync(Arg.Any<IEnumerable<DeleteUserValidationRequest>>())
            .Returns(validationResults);
    }

    private static async Task AssertSuccessfulUserOperations(
        SutProvider<DeleteClaimedOrganizationUserAccountCommand> sutProvider,
        IEnumerable<User> expectedUsers,
        IEnumerable<OrganizationUser> expectedOrgUsers)
    {
        var userList = expectedUsers.ToList();
        var orgUserList = expectedOrgUsers.ToList();

        await sutProvider.GetDependency<IUserRepository>().Received(1)
            .DeleteManyAsync(Arg.Is<IEnumerable<User>>(users =>
                userList.All(expectedUser => users.Any(u => u.Id == expectedUser.Id))));

        foreach (var user in userList)
        {
            await sutProvider.GetDependency<IPushNotificationService>().Received(1).PushLogOutAsync(user.Id);
        }

        await sutProvider.GetDependency<IEventService>().Received(1)
            .LogOrganizationUserEventsAsync(Arg.Is<IEnumerable<(OrganizationUser, EventType, DateTime?)>>(events =>
                orgUserList.All(expectedOrgUser =>
                    events.Any(e => e.Item1.Id == expectedOrgUser.Id && e.Item2 == EventType.OrganizationUser_Deleted))));
    }

    private static async Task AssertNoUserOperations(SutProvider<DeleteClaimedOrganizationUserAccountCommand> sutProvider)
    {
        await sutProvider.GetDependency<IUserRepository>().DidNotReceiveWithAnyArgs().DeleteManyAsync(default);
        await sutProvider.GetDependency<IPushNotificationService>().DidNotReceiveWithAnyArgs().PushLogOutAsync(default);
        await sutProvider.GetDependency<IEventService>().DidNotReceiveWithAnyArgs()
            .LogOrganizationUserEventsAsync(default(IEnumerable<(OrganizationUser, EventType, DateTime?)>));
    }
}
