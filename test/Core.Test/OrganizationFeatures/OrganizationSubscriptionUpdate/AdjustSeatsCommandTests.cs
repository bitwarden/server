using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.OrganizationFeatures.OrganizationSubscriptionUpdate;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.OrganizationFeatures.OrganizationSubscriptionUpdate;
[SutProviderCustomize]
public class AdjustSeatsCommandTests
{

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

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdjustSeatsAsync(organization, seatAdjustment));

        Assert.Contains("No payment method found.", exception.Message);
    }

    [Theory]
    [BitAutoData]
    public async Task AdjustSeatsAsync_NoSubscription_ThrowsBadRequestException(
        Organization organization,
        int seatAdjustment,
        SutProvider<AdjustSeatsCommand> sutProvider)
    {
        organization.GatewaySubscriptionId = null;

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            sutProvider.Sut.AdjustSeatsAsync(organization, seatAdjustment));

        Assert.Contains("No subscription found.", exception.Message);
    }

}

