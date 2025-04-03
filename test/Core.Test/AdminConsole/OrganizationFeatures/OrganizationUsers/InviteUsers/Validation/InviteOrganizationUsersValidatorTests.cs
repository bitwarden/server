using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Models.Business;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;
using Bit.Core.Billing.Models.StaticStore.Plans;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptions.Interface;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;
using OrganizationUserInvite = Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Models.OrganizationUserInvite;

namespace Bit.Core.Test.AdminConsole.OrganizationFeatures.OrganizationUsers.InviteUsers.Validation;

[SutProviderCustomize]
public class InviteOrganizationUsersValidatorTests
{
    [Theory]
    [BitAutoData]
    public async Task ValidateAsync_WhenOrganizationHasSecretsManagerInvites_ThenShouldCorrectlyCalculateSeatsToAdd(
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
                new OrganizationUserInvite(
                    email: "test@email.com",
                    externalId: "test-external-id"),
                new OrganizationUserInvite(
                    email: "test2@email.com",
                    externalId: "test-external-id2"),
                new OrganizationUserInvite(
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

        sutProvider.GetDependency<IUpdateSecretsManagerSubscriptionCommand>()
            .Received(1)
            .ValidateUpdateAsync(Arg.Is<SecretsManagerSubscriptionUpdate>(x =>
                x.SmSeatsChanged == true && x.SmSeats == 12));
    }
}
