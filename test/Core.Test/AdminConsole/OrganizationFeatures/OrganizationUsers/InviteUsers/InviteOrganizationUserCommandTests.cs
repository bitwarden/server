using System.Net.Mail;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Errors;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.PasswordManager;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.SecretsManager;
using Bit.Core.AdminConsole.Shared.Validation;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Commands;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
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

        var organizationDto = new InviteOrganization(organization);

        var request = new OrganizationUserSingleEmailInvite(user.Email,
            true,
            organizationDto,
            timeProvider.GetUtcNow(),
            externalId);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .SelectKnownEmailsAsync(organization.Id, Arg.Any<IEnumerable<string>>(), false)
            .Returns([user.Email]);

        sutProvider.GetDependency<IInviteUsersValidation>()
            .ValidateAsync(Arg.Any<InviteUserOrganizationValidationRequest>())
            .Returns(new Valid<InviteUserOrganizationValidationRequest>(GetInviteValidationRequestMock(request, organizationDto)));

        // Act
        var result = await sutProvider.Sut.InviteScimOrganizationUserAsync(request);

        // Assert
        Assert.IsType<Failure<ScimInviteOrganizationUsersResponse>>(result);
        Assert.Equal(InviteOrganizationUsersCommand.NoUsersToInvite, (result as Failure<ScimInviteOrganizationUsersResponse>).ErrorMessage);

        await sutProvider.GetDependency<IPaymentService>()
            .DidNotReceiveWithAnyArgs()
            .AdjustSeatsAsync(Arg.Any<Organization>(), Arg.Any<Plan>(), Arg.Any<int>());

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

        var organizationDto = new InviteOrganization(organization);

        var request = new OrganizationUserSingleEmailInvite(orgUser.Email,
            true,
            organizationDto,
            timeProvider.GetUtcNow(),
            externalId);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .SelectKnownEmailsAsync(organization.Id, Arg.Any<IEnumerable<string>>(), false)
            .Returns([]);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        sutProvider.GetDependency<IInviteUsersValidation>()
            .ValidateAsync(Arg.Any<InviteUserOrganizationValidationRequest>())
            .Returns(new Valid<InviteUserOrganizationValidationRequest>(GetInviteValidationRequestMock(request, organizationDto)));

        // Act
        var result = await sutProvider.Sut.InviteScimOrganizationUserAsync(request);

        // Assert
        Assert.IsType<Success<ScimInviteOrganizationUsersResponse>>(result);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .Received(1)
            .CreateManyAsync(Arg.Is<IEnumerable<CreateOrganizationUser>>(users =>
                users.Any(user => user.OrganizationUser.Email == request.Email)));

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

        user.Email = address.Address;

        var organizationDto = new InviteOrganization(organization);

        var request = new OrganizationUserSingleEmailInvite(user.Email,
            true,
            organizationDto,
            timeProvider.GetUtcNow(),
            externalId);

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .SelectKnownEmailsAsync(organization.Id, Arg.Any<IEnumerable<string>>(), false)
            .Returns([]);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        sutProvider.GetDependency<IInviteUsersValidation>()
            .ValidateAsync(Arg.Any<InviteUserOrganizationValidationRequest>())
            .Returns(new Invalid<InviteUserOrganizationValidationRequest>(new Error<InviteUserOrganizationValidationRequest>(errorMessage, new InviteUserOrganizationValidationRequest())));

        // Act
        var result = await sutProvider.Sut.InviteScimOrganizationUserAsync(request);

        // Assert
        Assert.IsType<Failure<ScimInviteOrganizationUsersResponse>>(result);
        var failure = result as Failure<ScimInviteOrganizationUsersResponse>;

        Assert.Equal(errorMessage, failure!.ErrorMessage);

        await sutProvider.GetDependency<IOrganizationUserRepository>()
            .DidNotReceive()
            .CreateManyAsync(Arg.Any<IEnumerable<CreateOrganizationUser>>());

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

        var organizationDto = new InviteOrganization(organization);

        var request = new OrganizationUserSingleEmailInvite(user.Email,
            true,
            organizationDto,
            timeProvider.GetUtcNow(),
            externalId);

        var orgUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        orgUserRepository
            .SelectKnownEmailsAsync(organizationDto.OrganizationId, Arg.Any<IEnumerable<string>>(), false)
            .Returns([]);
        orgUserRepository
            .GetManyByMinimumRoleAsync(organizationDto.OrganizationId, OrganizationUserType.Owner)
            .Returns([ownerDetails]);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        sutProvider.GetDependency<IInviteUsersValidation>()
            .ValidateAsync(Arg.Any<InviteUserOrganizationValidationRequest>())
            .Returns(new Valid<InviteUserOrganizationValidationRequest>(GetInviteValidationRequestMock(request, organizationDto)
                .WithPasswordManagerUpdate(new PasswordManagerSubscriptionUpdate(organizationDto, organization.Seats.Value, 1))));

        // Act
        var result = await sutProvider.Sut.InviteScimOrganizationUserAsync(request);

        // Assert
        Assert.IsType<Success<ScimInviteOrganizationUsersResponse>>(result);

        Assert.NotNull(organizationDto.MaxAutoScaleSeats);

        await sutProvider.GetDependency<IMailService>()
            .Received(1)
            .SendOrganizationMaxSeatLimitReachedEmailAsync(organization,
                organizationDto.MaxAutoScaleSeats.Value,
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

        var organizationDto = new InviteOrganization(organization);

        var request = new OrganizationUserSingleEmailInvite(user.Email,
            true,
            organizationDto,
            timeProvider.GetUtcNow(),
            externalId);

        var passwordManagerUpdate = new PasswordManagerSubscriptionUpdate(organizationDto, organization.Seats.Value, 1);

        var orgUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        orgUserRepository
            .SelectKnownEmailsAsync(organizationDto.OrganizationId, Arg.Any<IEnumerable<string>>(), false)
            .Returns([]);
        orgUserRepository
            .GetManyByMinimumRoleAsync(organizationDto.OrganizationId, OrganizationUserType.Owner)
            .Returns([ownerDetails]);

        var orgRepository = sutProvider.GetDependency<IOrganizationRepository>();

        orgRepository.GetByIdAsync(organization.Id)
            .Returns(organization);

        sutProvider.GetDependency<IInviteUsersValidation>()
            .ValidateAsync(Arg.Any<InviteUserOrganizationValidationRequest>())
            .Returns(new Valid<InviteUserOrganizationValidationRequest>(GetInviteValidationRequestMock(request, organizationDto)
                .WithPasswordManagerUpdate(passwordManagerUpdate)));

        // Act
        var result = await sutProvider.Sut.InviteScimOrganizationUserAsync(request);

        // Assert
        Assert.IsType<Success<ScimInviteOrganizationUsersResponse>>(result);

        await sutProvider.GetDependency<IPaymentService>()
            .AdjustSeatsAsync(organization, organizationDto.Plan, passwordManagerUpdate.SeatsRequiredToAdd);

        await orgRepository.Received(1).ReplaceAsync(Arg.Is<Organization>(x => x.Seats == passwordManagerUpdate.UpdatedSeatTotal));

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
        ownerDetails.Type = OrganizationUserType.Owner;

        var organizationDto = new InviteOrganization(organization);

        var request = new OrganizationUserSingleEmailInvite(user.Email,
            true,
            organizationDto,
            timeProvider.GetUtcNow(),
            externalId);

        var secretsManagerSubscriptionUpdate = new SecretsManagerSubscriptionUpdate(
            organizationDto,
            organization.SmSeats.Value,
            1,
            organization.Seats.Value);

        var orgUserRepository = sutProvider.GetDependency<IOrganizationUserRepository>();

        orgUserRepository
            .SelectKnownEmailsAsync(organizationDto.OrganizationId, Arg.Any<IEnumerable<string>>(), false)
            .Returns([]);
        orgUserRepository
            .GetManyByMinimumRoleAsync(organizationDto.OrganizationId, OrganizationUserType.Owner)
            .Returns([ownerDetails]);

        var orgRepository = sutProvider.GetDependency<IOrganizationRepository>();

        orgRepository.GetByIdAsync(organization.Id)
            .Returns(organization);

        sutProvider.GetDependency<IInviteUsersValidation>()
            .ValidateAsync(Arg.Any<InviteUserOrganizationValidationRequest>())
            .Returns(new Valid<InviteUserOrganizationValidationRequest>(GetInviteValidationRequestMock(request, organizationDto)
                .WithSecretsManagerUpdate(secretsManagerSubscriptionUpdate)));

        // Act
        var result = await sutProvider.Sut.InviteScimOrganizationUserAsync(request);

        // Assert;
        Assert.IsType<Success<ScimInviteOrganizationUsersResponse>>(result);

        await sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .Received(1)
            .UpdateSubscriptionAsync(Arg.Is<Core.Models.Business.SecretsManagerSubscriptionUpdate>(update =>
                update.SmSeats == secretsManagerSubscriptionUpdate.UpdatedSeatTotal));
    }
}
