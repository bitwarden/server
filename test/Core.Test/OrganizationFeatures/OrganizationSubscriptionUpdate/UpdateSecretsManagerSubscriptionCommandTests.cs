using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Business;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate;
using Bit.Core.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Services;
using Bit.Core.Test.AutoFixture.OrganizationFixtures;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSubscriptionUpdate;

[SutProviderCustomize]
public class UpdateSecretsManagerSubscriptionCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task UpdateSecretsManagerSubscription_NoOrganization_Throws(
        Guid organizationId,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationId)
            .Returns((Organization)null);

        var organizationUpdate = new OrganizationUpdate
        {
            OrganizationId = organizationId,
            MaxAutoscaleSeats = null,
            SeatAdjustment = 0
        };

        await Assert.ThrowsAsync<NotFoundException>(
            () => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
    }

    [Theory]
    [PaidOrganizationCustomize(CheckedPlanType = PlanType.EnterpriseAnnually)]
    [BitAutoData("Cannot set max seat autoscaling below seat count", 1, 0, 2)]
    [BitAutoData("Cannot set max seat autoscaling below seat count", 4, -1, 6)]
    public async Task Enterprise_UpdateSubscription_BadInputThrows(string expectedMessage,
        int? maxAutoscaleSeats, int seatAdjustment, int? currentSeats, Organization organization, SutProvider<OrganizationService> sutProvider)
        => await UpdateSubscription_BadInputThrows(expectedMessage, maxAutoscaleSeats, seatAdjustment, currentSeats, organization, sutProvider);
    [Theory]
    [FreeOrganizationCustomize]
    [BitAutoData("Your plan does not allow seat autoscaling", 10, 0, null)]
    public async Task Free_UpdateSubscription_BadInputThrows(string expectedMessage,
        int? maxAutoscaleSeats, int seatAdjustment, int? currentSeats, Organization organization, SutProvider<OrganizationService> sutProvider)
        => await UpdateSubscription_BadInputThrows(expectedMessage, maxAutoscaleSeats, seatAdjustment, currentSeats, organization, sutProvider);

    private async Task UpdateSubscription_BadInputThrows(string expectedMessage,
        int? maxAutoscaleSeats, int seatAdjustment, int? currentSeats, Organization organization, SutProvider<OrganizationService> sutProvider)
    {
        organization.Seats = currentSeats;
        sutProvider.GetDependency<IOrganizationRepository>().GetByIdAsync(organization.Id).Returns(organization);

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => sutProvider.Sut.UpdateSubscription(organization.Id,
            seatAdjustment, maxAutoscaleSeats));

        Assert.Contains(expectedMessage, exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSecretsManagerSubscription_SeatAdjustmentExceedsMaxAutoscaleSeats_Throws(
        Organization organization,
        OrganizationUpdate organizationUpdate,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.MaxAutoscaleSmSeats = 5;
        organization.SmSeats = 3;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationUpdate.OrganizationId)
            .Returns(organization);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
    }

    [Theory]
    [BitAutoData]
    public async Task UpdateSecretsManagerSubscription_ServiceAccountsAdjustmentExceedsMaxAutoscaleServiceAccounts_Throws(
        Organization organization,
        OrganizationUpdate organizationUpdate,
        SutProvider<UpdateSecretsManagerSubscriptionCommand> sutProvider)
    {
        organization.MaxAutoscaleSmServiceAccounts = 10;
        organization.SmServiceAccounts = 7;

        sutProvider.GetDependency<IOrganizationRepository>()
            .GetByIdAsync(organizationUpdate.OrganizationId)
            .Returns(organization);

        await Assert.ThrowsAsync<BadRequestException>(
            () => sutProvider.Sut.UpdateSecretsManagerSubscription(organizationUpdate));
    }



}
