using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Subscriptions.Entities;
using Bit.Core.Billing.Subscriptions.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Billing.Repositories;

public class SubscriptionDiscountRepositoryTests
{
    private static SubscriptionDiscount CreateTestDiscount(
        string? stripeCouponId = null,
        ICollection<string>? stripeProductIds = null,
        decimal? percentOff = null,
        long? amountOff = null,
        string? currency = null,
        string duration = "once",
        int? durationInMonths = null,
        string? name = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        DiscountAudienceType audienceType = DiscountAudienceType.UserHasNoPreviousSubscriptions,
        DateTime? creationDate = null,
        DateTime? revisionDate = null)
    {
        var now = DateTime.UtcNow;
        return new SubscriptionDiscount
        {
            StripeCouponId = stripeCouponId ?? $"test-{Guid.NewGuid()}",
            StripeProductIds = stripeProductIds,
            PercentOff = percentOff,
            AmountOff = amountOff,
            Currency = currency,
            Duration = duration,
            DurationInMonths = durationInMonths,
            Name = name,
            StartDate = startDate ?? now,
            EndDate = endDate ?? now.AddDays(30),
            AudienceType = audienceType,
            CreationDate = creationDate ?? now,
            RevisionDate = revisionDate ?? now
        };
    }

    [Theory, DatabaseData]
    public async Task GetActiveDiscountsAsync_ReturnsDiscountsWithinDateRange(
        ISubscriptionDiscountRepository subscriptionDiscountRepository)
    {
        // Create a discount that is currently active
        var activeDiscount = await subscriptionDiscountRepository.CreateAsync(
            CreateTestDiscount(
                stripeCouponId: $"test-active-{Guid.NewGuid()}",
                percentOff: 25.00m,
                name: "Active Discount",
                startDate: DateTime.UtcNow.AddDays(-1),
                endDate: DateTime.UtcNow.AddDays(30)));

        // Create a discount that has expired
        var expiredDiscount = await subscriptionDiscountRepository.CreateAsync(
            CreateTestDiscount(
                stripeCouponId: $"test-expired-{Guid.NewGuid()}",
                percentOff: 50.00m,
                name: "Expired Discount",
                startDate: DateTime.UtcNow.AddDays(-60),
                endDate: DateTime.UtcNow.AddDays(-30)));

        // Create a discount that starts in the future
        var futureDiscount = await subscriptionDiscountRepository.CreateAsync(
            CreateTestDiscount(
                stripeCouponId: $"test-future-{Guid.NewGuid()}",
                percentOff: 15.00m,
                name: "Future Discount",
                startDate: DateTime.UtcNow.AddDays(30),
                endDate: DateTime.UtcNow.AddDays(60)));

        // Act
        var activeDiscounts = await subscriptionDiscountRepository.GetActiveDiscountsAsync();

        // Assert
        Assert.Contains(activeDiscounts, d => d.Id == activeDiscount.Id);
        Assert.DoesNotContain(activeDiscounts, d => d.Id == expiredDiscount.Id);
        Assert.DoesNotContain(activeDiscounts, d => d.Id == futureDiscount.Id);
    }

    [Theory, DatabaseData]
    public async Task GetByStripeCouponIdAsync_ReturnsCorrectDiscount(
        ISubscriptionDiscountRepository subscriptionDiscountRepository)
    {
        // Arrange
        var couponId = $"test-coupon-{Guid.NewGuid()}";
        var discount = await subscriptionDiscountRepository.CreateAsync(
            CreateTestDiscount(
                stripeCouponId: couponId,
                stripeProductIds: new List<string> { "prod_123", "prod_456" },
                percentOff: 20.00m,
                duration: "repeating",
                durationInMonths: 3,
                name: "Test Discount",
                endDate: DateTime.UtcNow.AddDays(90)));

        // Act
        var result = await subscriptionDiscountRepository.GetByStripeCouponIdAsync(couponId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(discount.Id, result.Id);
        Assert.Equal(couponId, result.StripeCouponId);
        Assert.Equal(20.00m, result.PercentOff);
        Assert.Equal(3, result.DurationInMonths);
    }

    [Theory, DatabaseData]
    public async Task GetByStripeCouponIdAsync_ReturnsNull_WhenCouponDoesNotExist(
        ISubscriptionDiscountRepository subscriptionDiscountRepository)
    {
        // Act
        var result = await subscriptionDiscountRepository.GetByStripeCouponIdAsync("non-existent-coupon");

        // Assert
        Assert.Null(result);
    }

    [Theory, DatabaseData]
    public async Task CreateAsync_CreatesDiscountSuccessfully(
        ISubscriptionDiscountRepository subscriptionDiscountRepository)
    {
        // Arrange
        var discount = CreateTestDiscount(
            stripeCouponId: $"test-create-{Guid.NewGuid()}",
            stripeProductIds: new List<string> { "prod_789" },
            amountOff: 500,
            currency: "usd",
            name: "Fixed Amount Discount");

        // Act
        var createdDiscount = await subscriptionDiscountRepository.CreateAsync(discount);

        // Assert
        Assert.NotNull(createdDiscount);
        Assert.NotEqual(Guid.Empty, createdDiscount.Id);
        Assert.Equal(discount.StripeCouponId, createdDiscount.StripeCouponId);
        Assert.Equal(500, createdDiscount.AmountOff);
        Assert.Equal("usd", createdDiscount.Currency);
    }

    [Theory, DatabaseData]
    public async Task ReplaceAsync_UpdatesDiscountSuccessfully(
        ISubscriptionDiscountRepository subscriptionDiscountRepository)
    {
        // Arrange
        var discount = await subscriptionDiscountRepository.CreateAsync(
            CreateTestDiscount(
                stripeCouponId: $"test-update-{Guid.NewGuid()}",
                percentOff: 10.00m,
                name: "Original Name"));

        // Act
        discount.Name = "Updated Name";
        discount.PercentOff = 15.00m;
        discount.RevisionDate = DateTime.UtcNow;
        await subscriptionDiscountRepository.ReplaceAsync(discount);

        // Assert
        var updatedDiscount = await subscriptionDiscountRepository.GetByIdAsync(discount.Id);
        Assert.NotNull(updatedDiscount);
        Assert.Equal("Updated Name", updatedDiscount.Name);
        Assert.Equal(15.00m, updatedDiscount.PercentOff);
    }

    [Theory, DatabaseData]
    public async Task DeleteAsync_RemovesDiscountSuccessfully(
        ISubscriptionDiscountRepository subscriptionDiscountRepository)
    {
        // Arrange
        var discount = await subscriptionDiscountRepository.CreateAsync(
            CreateTestDiscount(
                stripeCouponId: $"test-delete-{Guid.NewGuid()}",
                percentOff: 25.00m,
                name: "To Be Deleted"));

        // Act
        await subscriptionDiscountRepository.DeleteAsync(discount);

        // Assert
        var deletedDiscount = await subscriptionDiscountRepository.GetByIdAsync(discount.Id);
        Assert.Null(deletedDiscount);
    }
}
