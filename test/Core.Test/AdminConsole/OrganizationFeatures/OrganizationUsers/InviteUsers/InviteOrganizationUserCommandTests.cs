using System.Data;
using System.Data.Common;
using System.Net.Mail;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Enums.Provider;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Errors;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.PasswordManager;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.Commands;
using Bit.Core.AdminConsole.Utilities.Errors;
using Bit.Core.AdminConsole.Utilities.Validation;
using Bit.Core.Billing.Models.StaticStore.Plans;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;
using static Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Helpers.InviteUserOrganizationValidationRequestHelpers;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;

[SutProviderCustomize]
public class InviteOrganizationUserCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task InviteScimOrganizationUserAsync_WhenEmailAlreadyExists_ThenNoInviteIsSentAndNoSeatsAreAdjusted(
        MailAddress address,
        Organization organization,
        OrganizationUser user,
        FakeTimeProvider timeProvider,
        string externalId,
        SutProvider<InviteOrganizationUsersCommand> sutProvider)
    {
        // Arrange
        user.Email = address.Address;

        var inviteOrganization = new InviteOrganization(organization, new FreePlan());
        var transaction = Substitute.For<DbTransaction>();
        transaction.IsolationLevel.Returns(IsolationLevel.ReadCommitted);

        var request = new InviteOrganizationUsersRequest(
            invites: [
                new OrganizationUserInviteCommandModel(
                    email: user.Email,
                    assignedCollections: [],
                    groups: [],
                    type: OrganizationUserType.User,
                    permissions: new Permissions(),
                    externalId: externalId,
                    accessSecretsManager: true)
            ],
            inviteOrganization: inviteOrganization,
            performedBy: Guid.Empty,
            timeProvider.GetUtcNow(),
            transaction: transaction);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .SelectKnownEmailsInTransactionAsync(organization.Id, Arg.Any<IEnumerable<string>>(), false,
                Arg.Any<DbTransaction>())
            .Returns([user.Email]);

        var validationMock = GetInviteValidationRequestMock(request, inviteOrganization, organization);

        sutProvider.GetDependency<IInviteUsersValidator>()
            .ValidateAsync(Arg.Any<InviteOrganizationUsersValidationRequest>())
            .Returns(new Valid<InviteOrganizationUsersValidationRequest>(validationMock));

        // Act
        var result = await sutProvider.Sut.InviteScimOrganizationUserAsync(request);

        // Assert
        Assert.IsType<Failure<ScimInviteOrganizationUsersResponse>>(result);
        Assert.Equal(NoUsersToInviteError.Code, (result as Failure<ScimInviteOrganizationUsersResponse>)!.Error.Message);

        await sutProvider.GetDependency<ISendOrganizationInvitesCommand>()
            .DidNotReceiveWithAnyArgs()
            .SendInvitesAsync(Arg.Any<SendInvitesRequest>());

        await sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .DidNotReceiveWithAnyArgs()
            .UpdateSubscriptionAsync(Arg.Any<Core.Models.Business.SecretsManagerSubscriptionUpdate>());
    }

    [Theory]
    [BitAutoData]
    public async Task InviteScimOrganizationUserAsync_WhenEmailDoesNotExistAndRequestIsValid_ThenUserIsSavedAndInviteIsSent(
            MailAddress address,
            Organization organization,
            OrganizationUser orgUser,
            FakeTimeProvider timeProvider,
            string externalId,
            SutProvider<InviteOrganizationUsersCommand> sutProvider)
    {
        // Arrange
        orgUser.Email = address.Address;

        var transaction = Substitute.For<DbTransaction>();
        transaction.IsolationLevel.Returns(IsolationLevel.ReadCommitted);

        var inviteOrganization = new InviteOrganization(organization, new FreePlan());

        var requestDate = timeProvider.GetUtcNow();

        var request = new InviteOrganizationUsersRequest(
            invites: [
                new OrganizationUserInviteCommandModel(
                    email: orgUser.Email,
                    assignedCollections: [],
                    groups: [],
                    type: OrganizationUserType.User,
                    permissions: new Permissions(),
                    externalId: externalId,
                    accessSecretsManager: true)
            ],
            inviteOrganization: inviteOrganization,
            performedBy: Guid.Empty,
            requestDate,
            transaction);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .SelectKnownEmailsInTransactionAsync(organization.Id, Arg.Any<IEnumerable<string>>(), false,
                Arg.Any<DbTransaction>())
            .Returns([]);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdInTransactionAsync(organization.Id, transaction)
            .Returns(organization);

        sutProvider.GetDependency<IInviteUsersValidator>()
            .ValidateAsync(Arg.Any<InviteOrganizationUsersValidationRequest>())
            .Returns(new Valid<InviteOrganizationUsersValidationRequest>(GetInviteValidationRequestMock(request, inviteOrganization, organization)));

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdInTransactionAsync(organization.Id,
                Arg.Any<DbTransaction>())
            .Returns(new OrganizationSeatCounts { Sponsored = 0, Users = 0 });

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetOccupiedSmSeatCountByOrganizationIdInTransactionAsync(organization.Id,
                Arg.Any<DbTransaction>())
            .Returns(0);

        // Act
        var result = await sutProvider.Sut.InviteScimOrganizationUserAsync(request);

        // Assert
        Assert.IsType<Success<ScimInviteOrganizationUsersResponse>>(result);

        await sutProvider.GetDependency<IOrganizationRepository>()
            .Received(1)
            .AddUsersToPasswordManagerAsync(organization.Id,
                requestDate.UtcDateTime,
                0,
                Arg.Is<IEnumerable<CreateOrganizationUser>>(users =>
                users.Any(user => user.OrganizationUser.Email == request.Invites.First().Email)),
                Arg.Any<DbTransaction>());

        await sutProvider.GetDependency<ISendOrganizationInvitesCommand>()
            .Received(1)
            .SendInvitesAsync(Arg.Is<SendInvitesRequest>(invite =>
                invite.Organization == organization &&
                invite.Users.Count(x => x.Email == orgUser.Email) == 1));
    }

    [Theory]
    [BitAutoData]
    public async Task InviteScimOrganizationUserAsync_WhenEmailIsNewAndRequestIsInvalid_ThenFailureIsReturnedWithValidationFailureReason(
            MailAddress address,
            Organization organization,
            OrganizationUser user,
            FakeTimeProvider timeProvider,
            string externalId,
            SutProvider<InviteOrganizationUsersCommand> sutProvider)
    {
        // Arrange
        const string errorMessage = "Org cannot add user for some given reason";

        var transaction = Substitute.For<DbTransaction>();
        transaction.IsolationLevel.Returns(IsolationLevel.ReadCommitted);

        user.Email = address.Address;

        var inviteOrganization = new InviteOrganization(organization, new FreePlan());

        var request = new InviteOrganizationUsersRequest(
            invites: [
                new OrganizationUserInviteCommandModel(
                    email: user.Email,
                    assignedCollections: [],
                    groups: [],
                    type: OrganizationUserType.User,
                    permissions: new Permissions(),
                    externalId: externalId,
                    accessSecretsManager: true)
            ],
            inviteOrganization: inviteOrganization,
            performedBy: Guid.Empty,
            timeProvider.GetUtcNow(),
            transaction);

        var validationRequest = GetInviteValidationRequestMock(request, inviteOrganization, organization);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .SelectKnownEmailsInTransactionAsync(organization.Id, Arg.Any<IEnumerable<string>>(), false, transaction)
            .Returns([]);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdInTransactionAsync(organization.Id, transaction)
            .Returns(organization);

        sutProvider.GetDependency<IInviteUsersValidator>()
            .ValidateAsync(Arg.Any<InviteOrganizationUsersValidationRequest>())
            .Returns(new Invalid<InviteOrganizationUsersValidationRequest>(
                new Error<InviteOrganizationUsersValidationRequest>(errorMessage, validationRequest)));

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdInTransactionAsync(organization.Id, transaction)
            .Returns(new OrganizationSeatCounts { Sponsored = 0, Users = 0 });

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetOccupiedSmSeatCountByOrganizationIdInTransactionAsync(organization.Id, transaction)
            .Returns(0);

        // Act
        var result = await sutProvider.Sut.InviteScimOrganizationUserAsync(request);

        // Assert
        Assert.IsType<Failure<ScimInviteOrganizationUsersResponse>>(result);
        var failure = result as Failure<ScimInviteOrganizationUsersResponse>;

        Assert.Equal(errorMessage, failure!.Error.Message);

        await sutProvider.GetDependency<ISendOrganizationInvitesCommand>()
            .DidNotReceive()
            .SendInvitesAsync(Arg.Any<SendInvitesRequest>());
    }

    [Theory]
    [BitAutoData]
    public async Task InviteScimOrganizationUserAsync_WhenValidInviteCausesOrganizationToReachMaxSeats_ThenOrganizationOwnersShouldBeNotified(
        MailAddress address,
        Organization organization,
        OrganizationUser user,
        FakeTimeProvider timeProvider,
        string externalId,
        OrganizationUserUserDetails ownerDetails,
        SutProvider<InviteOrganizationUsersCommand> sutProvider)
    {
        // Arrange
        user.Email = address.Address;
        organization.Seats = 1;
        organization.MaxAutoscaleSeats = 2;
        ownerDetails.Type = OrganizationUserType.Owner;
        var transaction = Substitute.For<DbTransaction>();
        transaction.IsolationLevel.Returns(IsolationLevel.ReadCommitted);

        var inviteOrganization = new InviteOrganization(organization, new FreePlan());

        var request = new InviteOrganizationUsersRequest(
            invites: [
                new OrganizationUserInviteCommandModel(
                    email: user.Email,
                    assignedCollections: [],
                    groups: [],
                    type: OrganizationUserType.User,
                    permissions: new Permissions(),
                    externalId: externalId,
                    accessSecretsManager: true)
            ],
            inviteOrganization: inviteOrganization,
            performedBy: Guid.Empty,
            timeProvider.GetUtcNow(),
            transaction);

        var orgUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        orgUserRepository
            .SelectKnownEmailsInTransactionAsync(inviteOrganization.OrganizationId, Arg.Any<IEnumerable<string>>(), false, transaction)
            .Returns([]);

        orgUserRepository
            .GetManyByMinimumRoleAsync(inviteOrganization.OrganizationId, OrganizationUserType.Owner)
            .Returns([ownerDetails]);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdInTransactionAsync(organization.Id, transaction)
            .Returns(organization);

        sutProvider.GetDependency<IInviteUsersValidator>()
            .ValidateAsync(Arg.Any<InviteOrganizationUsersValidationRequest>())
            .Returns(new Valid<InviteOrganizationUsersValidationRequest>(GetInviteValidationRequestMock(request, inviteOrganization, organization)
                .WithPasswordManagerUpdate(new PasswordManagerSubscriptionUpdate(inviteOrganization, organization.Seats.Value, 1))));

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdInTransactionAsync(organization.Id, transaction)
            .Returns(new OrganizationSeatCounts { Sponsored = 0, Users = 0 });

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetOccupiedSmSeatCountByOrganizationIdInTransactionAsync(organization.Id, transaction)
            .Returns(0);

        // Act
        var result = await sutProvider.Sut.InviteScimOrganizationUserAsync(request);

        // Assert
        Assert.IsType<Success<ScimInviteOrganizationUsersResponse>>(result);

        Assert.NotNull(inviteOrganization.MaxAutoScaleSeats);

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendOrganizationMaxSeatLimitReachedEmailAsync(organization,
                inviteOrganization.MaxAutoScaleSeats.Value,
                Arg.Is<IEnumerable<string>>(emails => emails.Any(email => email == ownerDetails.Email)));
    }

    [Theory]
    [BitAutoData]
    public async Task InviteScimOrganizationUserAsync_WhenValidInviteCausesOrgToAutoscale_ThenOrganizationOwnersShouldBeNotified(
            MailAddress address,
            Organization organization,
            OrganizationUser user,
            FakeTimeProvider timeProvider,
            string externalId,
            OrganizationUserUserDetails ownerDetails,
            SutProvider<InviteOrganizationUsersCommand> sutProvider)
    {
        // Arrange
        user.Email = address.Address;
        organization.Seats = 1;
        organization.MaxAutoscaleSeats = 2;
        organization.OwnersNotifiedOfAutoscaling = null;
        ownerDetails.Type = OrganizationUserType.Owner;
        var transaction = Substitute.For<DbTransaction>();
        transaction.IsolationLevel.Returns(IsolationLevel.ReadCommitted);

        var inviteOrganization = new InviteOrganization(organization, new Enterprise2019Plan(true));

        var request = new InviteOrganizationUsersRequest(
            invites:
            [
                new OrganizationUserInviteCommandModel(
                    email: user.Email,
                    assignedCollections: [],
                    groups: [],
                    type: OrganizationUserType.User,
                    permissions: new Permissions(),
                    externalId: externalId,
                    accessSecretsManager: true)
            ],
            inviteOrganization: inviteOrganization,
            performedBy: Guid.Empty,
            timeProvider.GetUtcNow(),
            transaction);

        var orgUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        orgUserRepository
            .SelectKnownEmailsInTransactionAsync(inviteOrganization.OrganizationId, Arg.Any<IEnumerable<string>>(), false, transaction)
            .Returns([]);
        orgUserRepository
            .GetManyByMinimumRoleAsync(inviteOrganization.OrganizationId, OrganizationUserType.Owner)
            .Returns([ownerDetails]);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdInTransactionAsync(organization.Id, transaction)
            .Returns(organization);

        sutProvider.GetDependency<IInviteUsersValidator>()
            .ValidateAsync(Arg.Any<InviteOrganizationUsersValidationRequest>())
            .Returns(new Valid<InviteOrganizationUsersValidationRequest>(
                GetInviteValidationRequestMock(request, inviteOrganization, organization)
                    .WithPasswordManagerUpdate(
                        new PasswordManagerSubscriptionUpdate(inviteOrganization, organization.Seats.Value, 1))));

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdInTransactionAsync(organization.Id, transaction)
            .Returns(new OrganizationSeatCounts { Sponsored = 0, Users = 0 });

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetOccupiedSmSeatCountByOrganizationIdInTransactionAsync(organization.Id, transaction)
            .Returns(0);

        // Act
        var result = await sutProvider.Sut.InviteScimOrganizationUserAsync(request);

        // Assert
        Assert.IsType<Success<ScimInviteOrganizationUsersResponse>>(result);

        Assert.NotNull(inviteOrganization.MaxAutoScaleSeats);

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendOrganizationAutoscaledEmailAsync(organization,
                inviteOrganization.Seats.Value,
                Arg.Is<IEnumerable<string>>(emails => emails.Any(email => email == ownerDetails.Email)));
    }

    [Theory]
    [BitAutoData]
    public async Task InviteScimOrganizationUserAsync_WhenValidInviteIncreasesSeats_ThenSeatTotalShouldBeUpdated(
        MailAddress address,
        Organization organization,
        OrganizationUser user,
        FakeTimeProvider timeProvider,
        string externalId,
        OrganizationUserUserDetails ownerDetails,
        SutProvider<InviteOrganizationUsersCommand> sutProvider)
    {
        // Arrange
        user.Email = address.Address;
        organization.Seats = 1;
        organization.MaxAutoscaleSeats = 2;
        ownerDetails.Type = OrganizationUserType.Owner;
        var transaction = Substitute.For<DbTransaction>();
        transaction.IsolationLevel.Returns(IsolationLevel.ReadCommitted);

        var plan = new FreePlan();
        var inviteOrganization = new InviteOrganization(organization, plan);

        var request = new InviteOrganizationUsersRequest(
            invites: [
                new OrganizationUserInviteCommandModel(
                    email: user.Email,
                    assignedCollections: [],
                    groups: [],
                    type: OrganizationUserType.User,
                    permissions: new Permissions(),
                    externalId: externalId,
                    accessSecretsManager: true)
            ],
            inviteOrganization: inviteOrganization,
            performedBy: Guid.Empty,
            timeProvider.GetUtcNow(),
            transaction);

        var passwordManagerUpdate = new PasswordManagerSubscriptionUpdate(inviteOrganization, organization.Seats.Value, 1);

        var orgUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        orgUserRepository
            .SelectKnownEmailsInTransactionAsync(inviteOrganization.OrganizationId, Arg.Any<IEnumerable<string>>(), false, transaction)
            .Returns([]);
        orgUserRepository
            .GetManyByMinimumRoleAsync(inviteOrganization.OrganizationId, OrganizationUserType.Owner)
            .Returns([ownerDetails]);

        var orgRepository = sutProvider.GetDependency<IOrganizationRepository>();

        orgRepository.GetByIdInTransactionAsync(organization.Id, transaction)
            .Returns(organization);

        sutProvider.GetDependency<IInviteUsersValidator>()
            .ValidateAsync(Arg.Any<InviteOrganizationUsersValidationRequest>())
            .Returns(new Valid<InviteOrganizationUsersValidationRequest>(GetInviteValidationRequestMock(request, inviteOrganization, organization)
                .WithPasswordManagerUpdate(passwordManagerUpdate)));

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdInTransactionAsync(organization.Id, transaction)
            .Returns(new OrganizationSeatCounts { Sponsored = 0, Users = 0 });

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetOccupiedSmSeatCountByOrganizationIdInTransactionAsync(organization.Id, transaction)
            .Returns(0);

        // Act
        var result = await sutProvider.Sut.InviteScimOrganizationUserAsync(request);

        // Assert
        Assert.IsType<Success<ScimInviteOrganizationUsersResponse>>(result);

        await orgRepository.Received(1).AddUsersToPasswordManagerAsync(organization.Id,
            request.PerformedAt.UtcDateTime,
            1,
            Arg.Any<IEnumerable<CreateOrganizationUser>>(),
            Arg.Any<DbTransaction>());

        await sutProvider.GetDependency<IApplicationCacheService>()
            .Received(1)
            .UpsertOrganizationAbilityAsync(Arg.Is<Organization>(x => x.Seats == passwordManagerUpdate.UpdatedSeatTotal));
    }

    [Theory]
    [BitAutoData]
    public async Task InviteScimOrganizationUserAsync_WhenValidInviteIncreasesSecretsManagerSeats_ThenSecretsManagerShouldBeUpdated(
    MailAddress address,
    Organization organization,
    OrganizationUser user,
    FakeTimeProvider timeProvider,
    string externalId,
    OrganizationUserUserDetails ownerDetails,
    SutProvider<InviteOrganizationUsersCommand> sutProvider)
    {
        // Arrange
        user.Email = address.Address;
        organization.Seats = 1;
        organization.SmSeats = 1;
        organization.MaxAutoscaleSeats = 2;
        organization.MaxAutoscaleSmSeats = 2;
        ownerDetails.Type = OrganizationUserType.Owner;
        var transaction = Substitute.For<DbTransaction>();
        transaction.IsolationLevel.Returns(IsolationLevel.ReadCommitted);

        var inviteOrganization = new InviteOrganization(organization, new FreePlan());

        var request = new InviteOrganizationUsersRequest(
            invites: [
                new OrganizationUserInviteCommandModel(
                    email: user.Email,
                    assignedCollections: [],
                    groups: [],
                    type: OrganizationUserType.User,
                    permissions: new Permissions(),
                    externalId: externalId,
                    accessSecretsManager: true)
            ],
            inviteOrganization: inviteOrganization,
            performedBy: Guid.Empty,
            timeProvider.GetUtcNow(),
            transaction);

        var secretsManagerSubscriptionUpdate = new SecretsManagerSubscriptionUpdate(organization, inviteOrganization.Plan, true)
            .AdjustSeats(request.Invites.Count(x => x.AccessSecretsManager));

        var orgUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();
        var orgRepository = sutProvider.GetDependency<IOrganizationRepository>();

        orgUserRepository
            .SelectKnownEmailsInTransactionAsync(inviteOrganization.OrganizationId, Arg.Any<IEnumerable<string>>(), false, transaction)
            .Returns([]);
        orgUserRepository
            .GetManyByMinimumRoleAsync(inviteOrganization.OrganizationId, OrganizationUserType.Owner)
            .Returns([ownerDetails]);
        orgRepository.GetOccupiedSeatCountByOrganizationIdInTransactionAsync(organization.Id, transaction).Returns(new OrganizationSeatCounts
        {
            Sponsored = 0,
            Users = 1
        });
        orgUserRepository.GetOccupiedSmSeatCountByOrganizationIdInTransactionAsync(organization.Id, transaction).Returns(1);

        orgRepository.GetByIdInTransactionAsync(organization.Id, transaction)
            .Returns(organization);

        sutProvider.GetDependency<IInviteUsersValidator>()
            .ValidateAsync(Arg.Any<InviteOrganizationUsersValidationRequest>())
            .Returns(new Valid<InviteOrganizationUsersValidationRequest>(GetInviteValidationRequestMock(request, inviteOrganization, organization)
                .WithSecretsManagerUpdate(secretsManagerSubscriptionUpdate)));

        // Act
        var result = await sutProvider.Sut.InviteScimOrganizationUserAsync(request);

        // Assert;
        Assert.IsType<Success<ScimInviteOrganizationUsersResponse>>(result);

        await sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .Received(1)
            .UpdateSubscriptionAsync(secretsManagerSubscriptionUpdate);
    }

    [Theory]
    [BitAutoData]
    public async Task InviteScimOrganizationUserAsync_WhenAnErrorOccursWhileInvitingUsers_ThenAnySeatChangesShouldBeReverted(
        MailAddress address,
        Organization organization,
        OrganizationUser user,
        FakeTimeProvider timeProvider,
        string externalId,
        OrganizationUserUserDetails ownerDetails,
        SutProvider<InviteOrganizationUsersCommand> sutProvider)
    {
        // Arrange
        user.Email = address.Address;
        organization.Seats = 1;
        organization.SmSeats = 1;
        organization.MaxAutoscaleSeats = 2;
        organization.MaxAutoscaleSmSeats = 2;
        ownerDetails.Type = OrganizationUserType.Owner;
        var transaction = Substitute.For<DbTransaction>();
        transaction.IsolationLevel.Returns(IsolationLevel.ReadCommitted);

        var inviteOrganization = new InviteOrganization(organization, new FreePlan());

        var request = new InviteOrganizationUsersRequest(
            invites: [
                new OrganizationUserInviteCommandModel(
                    email: user.Email,
                    assignedCollections: [],
                    groups: [],
                    type: OrganizationUserType.User,
                    permissions: new Permissions(),
                    externalId: externalId,
                    accessSecretsManager: true)
            ],
            inviteOrganization: inviteOrganization,
            performedBy: Guid.Empty,
            timeProvider.GetUtcNow(),
            transaction);

        var secretsManagerSubscriptionUpdate = new SecretsManagerSubscriptionUpdate(organization, inviteOrganization.Plan, true)
            .AdjustSeats(request.Invites.Count(x => x.AccessSecretsManager));

        var passwordManagerSubscriptionUpdate =
            new PasswordManagerSubscriptionUpdate(inviteOrganization, 1, request.Invites.Length);

        var orgUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        orgUserRepository
            .SelectKnownEmailsInTransactionAsync(inviteOrganization.OrganizationId, Arg.Any<IEnumerable<string>>(), false, transaction)
            .Returns([]);
        orgUserRepository
            .GetManyByMinimumRoleAsync(inviteOrganization.OrganizationId, OrganizationUserType.Owner)
            .Returns([ownerDetails]);

        var orgRepository = sutProvider.GetDependency<IOrganizationRepository>();

        orgRepository.GetByIdInTransactionAsync(organization.Id, transaction)
            .Returns(organization);

        sutProvider.GetDependency<IInviteUsersValidator>()
            .ValidateAsync(Arg.Any<InviteOrganizationUsersValidationRequest>())
            .Returns(new Valid<InviteOrganizationUsersValidationRequest>(GetInviteValidationRequestMock(request, inviteOrganization, organization)
                .WithPasswordManagerUpdate(passwordManagerSubscriptionUpdate)
                .WithSecretsManagerUpdate(secretsManagerSubscriptionUpdate)));

        sutProvider.GetDependency<ISendOrganizationInvitesCommand>()
            .SendInvitesAsync(Arg.Any<SendInvitesRequest>())
            .Throws(new Exception("Something went wrong"));

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdInTransactionAsync(organization.Id, transaction)
            .Returns(new OrganizationSeatCounts { Sponsored = 0, Users = 0 });

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetOccupiedSmSeatCountByOrganizationIdInTransactionAsync(organization.Id, transaction)
            .Returns(0);

        // Act
        var result = await sutProvider.Sut.InviteScimOrganizationUserAsync(request);

        // Assert
        Assert.IsType<Failure<ScimInviteOrganizationUsersResponse>>(result);
        Assert.Equal(FailedToInviteUsersError.Code, (result as Failure<ScimInviteOrganizationUsersResponse>)!.Error.Message);

        await transaction.Received(1).RollbackAsync();

        // SM revert
        await sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .Received(2)
            .UpdateSubscriptionAsync(Arg.Any<SecretsManagerSubscriptionUpdate>());

        await sutProvider.GetDependency<IApplicationCacheService>().Received(2)
            .UpsertOrganizationAbilityAsync(Arg.Any<Organization>());
    }

    [Theory]
    [BitAutoData]
    public async Task InviteScimOrganizationUserAsync_WhenAnOrganizationIsManagedByAProvider_ThenAnEmailShouldBeSentToTheProvider(
        MailAddress address,
        Organization organization,
        OrganizationUser user,
        FakeTimeProvider timeProvider,
        string externalId,
        OrganizationUserUserDetails ownerDetails,
        ProviderOrganization providerOrganization,
        SutProvider<InviteOrganizationUsersCommand> sutProvider)
    {
        // Arrange
        user.Email = address.Address;
        organization.Seats = 1;
        organization.SmSeats = 1;
        organization.MaxAutoscaleSeats = 2;
        organization.MaxAutoscaleSmSeats = 2;
        ownerDetails.Type = OrganizationUserType.Owner;
        var transaction = Substitute.For<DbTransaction>();
        transaction.IsolationLevel.Returns(IsolationLevel.ReadCommitted);

        providerOrganization.OrganizationId = organization.Id;

        var inviteOrganization = new InviteOrganization(organization, new FreePlan());

        var request = new InviteOrganizationUsersRequest(
            invites: [
                new OrganizationUserInviteCommandModel(
                    email: user.Email,
                    assignedCollections: [],
                    groups: [],
                    type: OrganizationUserType.User,
                    permissions: new Permissions(),
                    externalId: externalId,
                    accessSecretsManager: true)
            ],
            inviteOrganization: inviteOrganization,
            performedBy: Guid.Empty,
            timeProvider.GetUtcNow(),
            transaction);

        var secretsManagerSubscriptionUpdate = new SecretsManagerSubscriptionUpdate(organization, inviteOrganization.Plan, true)
            .AdjustSeats(request.Invites.Count(x => x.AccessSecretsManager));

        var passwordManagerSubscriptionUpdate =
            new PasswordManagerSubscriptionUpdate(inviteOrganization, 1, request.Invites.Length);

        var orgUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        orgUserRepository
            .SelectKnownEmailsInTransactionAsync(inviteOrganization.OrganizationId, Arg.Any<IEnumerable<string>>(), false, transaction)
            .Returns([]);
        orgUserRepository
            .GetManyByMinimumRoleAsync(inviteOrganization.OrganizationId, OrganizationUserType.Owner)
            .Returns([ownerDetails]);

        var orgRepository = sutProvider.GetDependency<IOrganizationRepository>();

        orgRepository.GetByIdInTransactionAsync(organization.Id, transaction)
            .Returns(organization);

        sutProvider.GetDependency<IInviteUsersValidator>()
            .ValidateAsync(Arg.Any<InviteOrganizationUsersValidationRequest>())
            .Returns(new Valid<InviteOrganizationUsersValidationRequest>(GetInviteValidationRequestMock(request, inviteOrganization, organization)
                .WithPasswordManagerUpdate(passwordManagerSubscriptionUpdate)
                .WithSecretsManagerUpdate(secretsManagerSubscriptionUpdate)));

        sutProvider.GetDependency<IProviderOrganizationRepository>()
            .GetByOrganizationId(organization.Id)
            .Returns(providerOrganization);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyDetailsByProviderAsync(providerOrganization.ProviderId, ProviderUserStatusType.Confirmed)
            .Returns(new List<ProviderUserUserDetails>
            {
                new()
                {
                    Email = "provider@email.com"
                }
            });

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdInTransactionAsync(organization.Id, transaction)
            .Returns(new OrganizationSeatCounts { Sponsored = 0, Users = 0 });

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetOccupiedSmSeatCountByOrganizationIdInTransactionAsync(organization.Id, transaction)
            .Returns(0);

        // Act
        var result = await sutProvider.Sut.InviteScimOrganizationUserAsync(request);

        // Assert
        Assert.IsType<Success<ScimInviteOrganizationUsersResponse>>(result);

        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendOrganizationMaxSeatLimitReachedEmailAsync(organization, 2,
                Arg.Is<IEnumerable<string>>(emails => emails.Any(email => email == "provider@email.com")));
    }

    [Theory]
    [BitAutoData]
    public async Task InviteScimOrganizationUserAsync_WhenAnOrganizationIsManagedByAProviderAndAutoscaleOccurs_ThenAnEmailShouldBeSentToTheProvider(
        MailAddress address,
        Organization organization,
        OrganizationUser user,
        FakeTimeProvider timeProvider,
        string externalId,
        OrganizationUserUserDetails ownerDetails,
        ProviderOrganization providerOrganization,
        SutProvider<InviteOrganizationUsersCommand> sutProvider)
    {
        // Arrange
        user.Email = address.Address;
        organization.Seats = 1;
        organization.SmSeats = 1;
        organization.MaxAutoscaleSeats = 2;
        organization.MaxAutoscaleSmSeats = 2;
        organization.OwnersNotifiedOfAutoscaling = null;
        ownerDetails.Type = OrganizationUserType.Owner;
        var transaction = Substitute.For<DbTransaction>();
        transaction.IsolationLevel.Returns(IsolationLevel.ReadCommitted);

        providerOrganization.OrganizationId = organization.Id;

        var inviteOrganization = new InviteOrganization(organization, new Enterprise2019Plan(true));

        var request = new InviteOrganizationUsersRequest(
            invites: [
                new OrganizationUserInviteCommandModel(
                    email: user.Email,
                    assignedCollections: [],
                    groups: [],
                    type: OrganizationUserType.User,
                    permissions: new Permissions(),
                    externalId: externalId,
                    accessSecretsManager: true)
            ],
            inviteOrganization: inviteOrganization,
            performedBy: Guid.Empty,
            timeProvider.GetUtcNow(),
            transaction);

        var secretsManagerSubscriptionUpdate = new SecretsManagerSubscriptionUpdate(organization, inviteOrganization.Plan, true)
            .AdjustSeats(request.Invites.Count(x => x.AccessSecretsManager));

        var passwordManagerSubscriptionUpdate =
            new PasswordManagerSubscriptionUpdate(inviteOrganization, 1, request.Invites.Length);

        var orgUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        orgUserRepository
            .SelectKnownEmailsInTransactionAsync(inviteOrganization.OrganizationId, Arg.Any<IEnumerable<string>>(), false, transaction)
            .Returns([]);
        orgUserRepository
            .GetManyByMinimumRoleAsync(inviteOrganization.OrganizationId, OrganizationUserType.Owner)
            .Returns([ownerDetails]);

        var orgRepository = sutProvider.GetDependency<IOrganizationRepository>();

        orgRepository.GetByIdInTransactionAsync(organization.Id, transaction)
            .Returns(organization);

        sutProvider.GetDependency<IInviteUsersValidator>()
            .ValidateAsync(Arg.Any<InviteOrganizationUsersValidationRequest>())
            .Returns(new Valid<InviteOrganizationUsersValidationRequest>(GetInviteValidationRequestMock(request, inviteOrganization, organization)
                .WithPasswordManagerUpdate(passwordManagerSubscriptionUpdate)
                .WithSecretsManagerUpdate(secretsManagerSubscriptionUpdate)));

        sutProvider.GetDependency<IProviderOrganizationRepository>()
            .GetByOrganizationId(organization.Id)
            .Returns(providerOrganization);

        sutProvider.GetDependency<IProviderUserRepository>()
            .GetManyDetailsByProviderAsync(providerOrganization.ProviderId, ProviderUserStatusType.Confirmed)
            .Returns(new List<ProviderUserUserDetails>
            {
                new()
                {
                    Email = "provider@email.com"
                }
            });

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdInTransactionAsync(organization.Id, transaction)
            .Returns(new OrganizationSeatCounts { Sponsored = 0, Users = 0 });

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetOccupiedSmSeatCountByOrganizationIdInTransactionAsync(organization.Id, transaction)
            .Returns(0);

        // Act
        var result = await sutProvider.Sut.InviteScimOrganizationUserAsync(request);

        // Assert
        Assert.IsType<Success<ScimInviteOrganizationUsersResponse>>(result);

        await sutProvider.GetDependency<IMailService>().Received(1)
            .SendOrganizationAutoscaledEmailAsync(organization, 1,
                Arg.Is<IEnumerable<string>>(emails => emails.Any(email => email == "provider@email.com")));
    }

    [Theory]
    [BitAutoData]
    public async Task InviteScimOrganizationUserAsync_WhenAnOrganizationAutoscalesButOwnersHaveAlreadyBeenNotified_ThenAnEmailShouldNotBeSent(
        MailAddress address,
        Organization organization,
        OrganizationUser user,
        FakeTimeProvider timeProvider,
        string externalId,
        OrganizationUserUserDetails ownerDetails,
        SutProvider<InviteOrganizationUsersCommand> sutProvider)
    {
        // Arrange
        user.Email = address.Address;
        organization.Seats = 1;
        organization.MaxAutoscaleSeats = 2;
        organization.OwnersNotifiedOfAutoscaling = DateTime.UtcNow;
        ownerDetails.Type = OrganizationUserType.Owner;
        var transaction = Substitute.For<DbTransaction>();
        transaction.IsolationLevel.Returns(IsolationLevel.ReadCommitted);

        var inviteOrganization = new InviteOrganization(organization, new Enterprise2019Plan(true));

        var request = new InviteOrganizationUsersRequest(
            invites:
            [
                new OrganizationUserInviteCommandModel(
                    email: user.Email,
                    assignedCollections: [],
                    groups: [],
                    type: OrganizationUserType.User,
                    permissions: new Permissions(),
                    externalId: externalId,
                    accessSecretsManager: true)
            ],
            inviteOrganization: inviteOrganization,
            performedBy: Guid.Empty,
            timeProvider.GetUtcNow(),
            transaction);

        var orgUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        orgUserRepository
            .SelectKnownEmailsInTransactionAsync(inviteOrganization.OrganizationId, Arg.Any<IEnumerable<string>>(), false, transaction)
            .Returns([]);
        orgUserRepository
            .GetManyByMinimumRoleAsync(inviteOrganization.OrganizationId, OrganizationUserType.Owner)
            .Returns([ownerDetails]);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdInTransactionAsync(organization.Id, transaction)
            .Returns(organization);

        sutProvider.GetDependency<IInviteUsersValidator>()
            .ValidateAsync(Arg.Any<InviteOrganizationUsersValidationRequest>())
            .Returns(new Valid<InviteOrganizationUsersValidationRequest>(
                GetInviteValidationRequestMock(request, inviteOrganization, organization)
                    .WithPasswordManagerUpdate(
                        new PasswordManagerSubscriptionUpdate(inviteOrganization, organization.Seats.Value, 1))));

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdInTransactionAsync(organization.Id, transaction)
            .Returns(new OrganizationSeatCounts { Sponsored = 0, Users = 0 });

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetOccupiedSmSeatCountByOrganizationIdInTransactionAsync(organization.Id, transaction)
            .Returns(0);

        // Act
        var result = await sutProvider.Sut.InviteScimOrganizationUserAsync(request);

        // Assert
        Assert.IsType<Success<ScimInviteOrganizationUsersResponse>>(result);

        Assert.NotNull(inviteOrganization.MaxAutoScaleSeats);

        await sutProvider.GetDependency<IMailService>()
            .DidNotReceive()
            .SendOrganizationAutoscaledEmailAsync(Arg.Any<Organization>(),
                Arg.Any<int>(),
                Arg.Any<IEnumerable<string>>());
    }

    [Theory]
    [BitAutoData]
    public async Task InviteScimOrganizationUserAsync_WhenAnOrganizationDoesNotAutoScale_ThenAnEmailShouldNotBeSent(
        MailAddress address,
        Organization organization,
        OrganizationUser user,
        FakeTimeProvider timeProvider,
        string externalId,
        OrganizationUserUserDetails ownerDetails,
        SutProvider<InviteOrganizationUsersCommand> sutProvider)
    {
        // Arrange
        user.Email = address.Address;
        organization.Seats = 2;
        organization.MaxAutoscaleSeats = 2;
        organization.OwnersNotifiedOfAutoscaling = DateTime.UtcNow;
        ownerDetails.Type = OrganizationUserType.Owner;
        var transaction = Substitute.For<DbTransaction>();
        transaction.IsolationLevel.Returns(IsolationLevel.ReadCommitted);

        var inviteOrganization = new InviteOrganization(organization, new Enterprise2019Plan(true));

        var request = new InviteOrganizationUsersRequest(
            invites:
            [
                new OrganizationUserInviteCommandModel(
                    email: user.Email,
                    assignedCollections: [],
                    groups: [],
                    type: OrganizationUserType.User,
                    permissions: new Permissions(),
                    externalId: externalId,
                    accessSecretsManager: true)
            ],
            inviteOrganization: inviteOrganization,
            performedBy: Guid.Empty,
            timeProvider.GetUtcNow(),
            transaction);

        var orgUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        orgUserRepository
            .SelectKnownEmailsInTransactionAsync(inviteOrganization.OrganizationId, Arg.Any<IEnumerable<string>>(), false, transaction)
            .Returns([]);
        orgUserRepository
            .GetManyByMinimumRoleAsync(inviteOrganization.OrganizationId, OrganizationUserType.Owner)
            .Returns([ownerDetails]);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdInTransactionAsync(organization.Id, transaction)
            .Returns(organization);

        sutProvider.GetDependency<IInviteUsersValidator>()
            .ValidateAsync(Arg.Any<InviteOrganizationUsersValidationRequest>())
            .Returns(new Valid<InviteOrganizationUsersValidationRequest>(
                GetInviteValidationRequestMock(request, inviteOrganization, organization)
                    .WithPasswordManagerUpdate(
                        new PasswordManagerSubscriptionUpdate(inviteOrganization, organization.Seats.Value, 1))));

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetOccupiedSeatCountByOrganizationIdInTransactionAsync(organization.Id, transaction)
            .Returns(new OrganizationSeatCounts { Sponsored = 0, Users = 0 });

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetOccupiedSmSeatCountByOrganizationIdInTransactionAsync(organization.Id, transaction)
            .Returns(0);

        // Act
        var result = await sutProvider.Sut.InviteScimOrganizationUserAsync(request);

        // Assert
        Assert.IsType<Success<ScimInviteOrganizationUsersResponse>>(result);

        Assert.NotNull(inviteOrganization.MaxAutoScaleSeats);

        await sutProvider.GetDependency<IMailService>()
            .DidNotReceive()
            .SendOrganizationAutoscaledEmailAsync(Arg.Any<Organization>(),
                Arg.Any<int>(),
                Arg.Any<IEnumerable<string>>());
    }
}
