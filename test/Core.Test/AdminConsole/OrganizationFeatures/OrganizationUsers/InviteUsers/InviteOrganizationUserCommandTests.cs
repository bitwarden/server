using System.Net.Mail;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation.Models;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Commands;
using Bit.Core.Models.Data;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;

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

        var organizationDto = OrganizationDto.FromOrganization(organization);

        var request = InviteScimOrganizationUserRequest.Create(
            OrganizationUserSingleEmailInvite.Create(
                user.Email,
                [],
                OrganizationUserType.User,
                new Permissions(),
                false),
            organizationDto,
            timeProvider.GetUtcNow(),
            externalId
        );

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .SelectKnownEmailsAsync(organization.Id, Arg.Any<IEnumerable<string>>(), false)
            .Returns([user.Email]);

        var validationRequest = new InviteUserOrganizationValidationRequest
        {
            Invites = [],
            Organization = organizationDto,
            PerformedBy = Guid.Empty,
            PerformedAt = request.PerformedAt,
            OccupiedPmSeats = 0,
            OccupiedSmSeats = 0,
            PasswordManagerSubscriptionUpdate = PasswordManagerSubscriptionUpdate.Create(organizationDto, 0, 0),
            SecretsManagerSubscriptionUpdate = SecretsManagerSubscriptionUpdate.Create(organizationDto, 0, 0, 0)
        };

        sutProvider.GetDependency<IInviteUsersValidation>()
            .ValidateAsync(Arg.Any<InviteUserOrganizationValidationRequest>())
            .Returns(new Valid<InviteUserOrganizationValidationRequest>(validationRequest));

        // Act
        var result = await sutProvider.Sut.InviteScimOrganizationUserAsync(request);

        // Assert
        Assert.IsType<Success<OrganizationUser>>(result);

        sutProvider.GetDependency<IPaymentService>()
            .DidNotReceiveWithAnyArgs()
            .AdjustSeatsAsync(Arg.Any<Organization>(), Arg.Any<Plan>(), Arg.Any<int>());

        sutProvider.GetDependency<ISendOrganizationInvitesCommand>()
            .DidNotReceiveWithAnyArgs()
            .SendInvitesAsync(Arg.Any<SendInvitesRequest>());

        sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .DidNotReceiveWithAnyArgs()
            .UpdateSubscriptionAsync(Arg.Any<Core.Models.Business.SecretsManagerSubscriptionUpdate>());
    }

}
