using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;
using Bit.Core.AdminConsole.Utilities.Validation;
using Bit.Core.Exceptions;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Test.Billing.Mocks.Plans;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;

[SutProviderCustomize]
public class InviteOrganizationUsersValidatorTests
{
    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenOrganizationHasSecretsManagerInvitesAndDoesNotHaveEnoughSeatsAvailable_ThenShouldCorrectlyCalculateSeatsToAdd(
        Organization organization,
        SutProvider<InviteOrganizationUsersValidator> sutProvider
    )
    {
        organization.Seats = null;
        organization.SmSeats = 10;
        organization.UseSecretsManager = true;

        var request = new InviteOrganizationUsersValidationRequest
        {
            Invites =
            [
                new OrganizationUserInviteCommandModel(
                    email: "test@email.com",
                    externalId: "test-external-id"),
                new OrganizationUserInviteCommandModel(
                    email: "test2@email.com",
                    externalId: "test-external-id2"),
                new OrganizationUserInviteCommandModel(
                    email: "test3@email.com",
                    externalId: "test-external-id3")
            ],
            InviteOrganization = new InviteOrganization(organization, new Enterprise2023Plan(true)),
            OccupiedPmSeats = 0,
            OccupiedSmSeats = 9
        };

        sutProvider.GetDependency<IPaymentService>()
            .HasSecretsManagerStandalone(request.InviteOrganization)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        _ = await sutProvider.Sut.ValidateAsync(request);

        await sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .Received(1)
            .ValidateUpdateAsync(Arg.Is<SecretsManagerSubscriptionUpdate>(x =>
                x.SmSeatsChanged == true && x.SmSeats == 12));
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenOrganizationHasSecretsManagerInvitesAndHasSeatsAvailable_ThenShouldReturnValid(
        Organization organization,
        SutProvider<InviteOrganizationUsersValidator> sutProvider
    )
    {
        organization.Seats = null;
        organization.SmSeats = 12;
        organization.UseSecretsManager = true;

        var request = new InviteOrganizationUsersValidationRequest
        {
            Invites =
            [
                new OrganizationUserInviteCommandModel(
                    email: "test@email.com",
                    externalId: "test-external-id"),
                new OrganizationUserInviteCommandModel(
                    email: "test2@email.com",
                    externalId: "test-external-id2"),
                new OrganizationUserInviteCommandModel(
                    email: "test3@email.com",
                    externalId: "test-external-id3")
            ],
            InviteOrganization = new InviteOrganization(organization, new Enterprise2023Plan(true)),
            OccupiedPmSeats = 0,
            OccupiedSmSeats = 9
        };

        sutProvider.GetDependency<IPaymentService>()
            .HasSecretsManagerStandalone(request.InviteOrganization)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.IsType<Valid<InviteOrganizationUsersValidationRequest>>(result);
    }

    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenOrganizationHasSecretsManagerInvitesAndSmSeatUpdateFailsValidation_ThenShouldReturnInvalid(
        Organization organization,
        SutProvider<InviteOrganizationUsersValidator> sutProvider
    )
    {
        organization.Seats = null;
        organization.SmSeats = 5;
        organization.MaxAutoscaleSmSeats = 5;
        organization.UseSecretsManager = true;

        var request = new InviteOrganizationUsersValidationRequest
        {
            Invites =
            [
                new OrganizationUserInviteCommandModel(
                    email: "test@email.com",
                    externalId: "test-external-id"),
                new OrganizationUserInviteCommandModel(
                    email: "test2@email.com",
                    externalId: "test-external-id2"),
                new OrganizationUserInviteCommandModel(
                    email: "test3@email.com",
                    externalId: "test-external-id3")
            ],
            InviteOrganization = new InviteOrganization(organization, new Enterprise2023Plan(true)),
            OccupiedPmSeats = 0,
            OccupiedSmSeats = 4
        };

        sutProvider.GetDependency<IPaymentService>()
            .HasSecretsManagerStandalone(request.InviteOrganization)
            .Returns(true);

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organization.Id)
            .Returns(organization);

        sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .ValidateUpdateAsync(Arg.Any<SecretsManagerSubscriptionUpdate>())
            .Throws(new BadRequestException("Some Secrets Manager Failure"));

        var result = await sutProvider.Sut.ValidateAsync(request);

        Assert.IsType<Invalid<InviteOrganizationUsersValidationRequest>>(result);
        Assert.Equal("Some Secrets Manager Failure", (result as Invalid<InviteOrganizationUsersValidationRequest>)!.Error.Message);
    }
}
