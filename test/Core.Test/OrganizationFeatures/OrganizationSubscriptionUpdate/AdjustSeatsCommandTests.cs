using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.StaticStore;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate;
using Bit.Core.Repositories;
using Bit.Core.SecretsManager.Repositories;
using Bit.Core.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSubscriptionUpdate;
[SutProviderCustomize]
public class AdjustSeatsCommandTests
{
    [Theory]
    [BitAutoData]
    public async Task AdjustServiceAccountAsync_ValidAdjustment_NoExceptions(
        Organization organization,
        int seatsAdjustment,
        SutProvider<AdjustSeatsCommand> sutProvider)
    {
        var plan = StaticStore.GetSecretsManagerPlan(PlanType.EnterpriseAnnually);
        plan.Type = organization.PlanType;
        plan.HasAdditionalSeatsOption = true;
        plan.BaseSeats = 5;
        plan.MaxAdditionalSeats = Int16.MaxValue;

        organization.SmSeats = 3;

        sutProvider.GetDependency<IPaymentService>()
            .AdjustSeatsAsync(Arg.Any<Organization>(), Arg.Any<Plan>(), Arg.Any<int>(), Arg.Any<DateTime?>())
            .Returns("paymentIntentClientSecret");

        var result = await sutProvider.Sut.AdjustSeatsAsync(organization, seatsAdjustment);

        Assert.NotNull(result);
        Assert.Equal("paymentIntentClientSecret", result);
    }

    [Theory]
    [BitAutoData]
    public async Task AdjustSeatsAsync_NoSeatLimit_ThrowsBadRequestException(
        Organization organization,
        int seatAdjustment,
        SutProvider<AdjustSeatsCommand> sutProvider)
    {
        organization.SmSeats = null;
        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdjustSeatsAsync(organization, seatAdjustment));
    }

    [Theory]
    [BitAutoData]
    public async Task AdjustSeatsAsync_NoPaymentMethod_ThrowsBadRequestException(
        Organization organization,
        int seatAdjustment,
        SutProvider<AdjustSeatsCommand> sutProvider)
    {
        organization.GatewayCustomerId = null;

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdjustSeatsAsync(organization, seatAdjustment));
    }

    [Theory]
    [BitAutoData]
    public async Task AdjustSeatsAsync_NoSubscription_ThrowsBadRequestException(
        Organization organization,
        int seatAdjustment,
        SutProvider<AdjustSeatsCommand> sutProvider)
    {
        organization.GatewaySubscriptionId = null;

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdjustSeatsAsync(organization, seatAdjustment));
    }

    [Theory]
    [BitAutoData]
    public async Task AdjustSeatsAsync_PlanDoesNotAllowAdditionalSeats_ThrowsBadRequestException(
        Organization organization,
        int seatAdjustment,
        SutProvider<AdjustSeatsCommand> sutProvider)
    {
        var plan = StaticStore.GetSecretsManagerPlan(PlanType.EnterpriseAnnually);
        plan.Type = organization.PlanType;
        plan.HasAdditionalSeatsOption = false;

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdjustSeatsAsync(organization, seatAdjustment));
    }

    [Theory]
    [BitAutoData]
    public async Task AdjustSeatsAsync_OccupiedSeatsExceedsNewCount_ThrowsBadRequestException(
        Organization organization,
        int seatAdjustment,
        int occupiedSeats,
        SutProvider<AdjustSeatsCommand> sutProvider)
    {
        var plan = StaticStore.GetSecretsManagerPlan(PlanType.EnterpriseAnnually);
        plan.Type = organization.PlanType;
        plan.HasAdditionalSeatsOption = true;
        plan.BaseSeats = 5;
        plan.MaxAdditionalSeats = 10;

        organization.SmSeats = 10;

        sutProvider.GetDependency<IOrganizationUserRepository>()
            .GetOccupiedSmSeatCountByOrganizationIdAsync(organization.Id)
            .Returns(occupiedSeats);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdjustSeatsAsync(organization, seatAdjustment));
    }
}

