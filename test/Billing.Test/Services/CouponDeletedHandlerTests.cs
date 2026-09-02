using Bit.Billing.Services.Implementations;
using Bit.Core.Billing.Subscriptions.Entities;
using Bit.Core.Billing.Subscriptions.Repositories;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Stripe;
using Xunit;

namespace Bit.Billing.Test.Services;

public class CouponDeletedHandlerTests
{
    private readonly ILogger<CouponDeletedHandler> _logger = Substitute.For<ILogger<CouponDeletedHandler>>();
    private readonly ISubscriptionDiscountRepository _subscriptionDiscountRepository = Substitute.For<ISubscriptionDiscountRepository>();
    private readonly CouponDeletedHandler _sut;

    public CouponDeletedHandlerTests()
    {
        _sut = new CouponDeletedHandler(_logger, _subscriptionDiscountRepository);
    }

    [Fact]
    public async Task HandleAsync_EventObjectNotCoupon_ReturnsWithoutDeleting()
    {
        // Arrange
        var stripeEvent = new Event
        {
            Id = "evt_test",
            Data = new EventData { Object = new Customer { Id = "cus_unexpected" } }
        };

        // Act
        await _sut.HandleAsync(stripeEvent);

        // Assert
        await _subscriptionDiscountRepository.DidNotReceiveWithAnyArgs()
            .GetByStripeCouponIdAsync(null!);
        await _subscriptionDiscountRepository.DidNotReceiveWithAnyArgs()
            .DeleteAsync(null!);
    }

    [Fact]
    public async Task HandleAsync_CouponNotInDatabase_DoesNotDeleteAnything()
    {
        // Arrange
        var stripeEvent = new Event { Data = new EventData { Object = new Coupon { Id = "cou_test" } } };

        _subscriptionDiscountRepository.GetByStripeCouponIdAsync("cou_test").Returns((SubscriptionDiscount?)null);

        // Act
        await _sut.HandleAsync(stripeEvent);

        // Assert
        await _subscriptionDiscountRepository.DidNotReceiveWithAnyArgs().DeleteAsync(null!);
    }

    [Fact]
    public async Task HandleAsync_CouponExistsInDatabase_DeletesDiscount()
    {
        // Arrange
        var stripeEvent = new Event { Data = new EventData { Object = new Coupon { Id = "cou_test" } } };

        var discount = new SubscriptionDiscount { StripeCouponId = "cou_test" };
        _subscriptionDiscountRepository.GetByStripeCouponIdAsync("cou_test").Returns(discount);

        // Act
        await _sut.HandleAsync(stripeEvent);

        // Assert
        await _subscriptionDiscountRepository.Received(1).DeleteAsync(discount);
    }
}
