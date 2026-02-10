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

        // Cleanup
        await subscriptionDiscountRepository.DeleteAsync(activeDiscount);
        await subscriptionDiscountRepository.DeleteAsync(expiredDiscount);
        await subscriptionDiscountRepository.DeleteAsync(futureDiscount);
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

        // Cleanup
        await subscriptionDiscountRepository.DeleteAsync(discount);
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

        // Cleanup
        await subscriptionDiscountRepository.DeleteAsync(createdDiscount);
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

        // Cleanup
        await subscriptionDiscountRepository.DeleteAsync(updatedDiscount);
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

    [Theory, DatabaseData]
    public async Task SearchAsync_ReturnsPagedResults_OrderedByCreationDateDescending(
        ISubscriptionDiscountRepository subscriptionDiscountRepository)
    {
        // Arrange - create discounts with future timestamps (should be at top)
        var farFuture = new DateTime(2500, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var discount1 = await subscriptionDiscountRepository.CreateAsync(
            CreateTestDiscount(
                stripeCouponId: $"test-search-1-{Guid.NewGuid()}",
                percentOff: 10.00m,
                name: "First Discount",
                creationDate: farFuture.AddSeconds(-3)));

        var discount2 = await subscriptionDiscountRepository.CreateAsync(
            CreateTestDiscount(
                stripeCouponId: $"test-search-2-{Guid.NewGuid()}",
                percentOff: 20.00m,
                name: "Second Discount",
                creationDate: farFuture.AddSeconds(-2)));

        var discount3 = await subscriptionDiscountRepository.CreateAsync(
            CreateTestDiscount(
                stripeCouponId: $"test-search-3-{Guid.NewGuid()}",
                percentOff: 30.00m,
                name: "Third Discount",
                creationDate: farFuture.AddSeconds(-1)));

        // Act - get first page
        var result = await subscriptionDiscountRepository.SearchAsync(0, 10);

        // Assert
        Assert.NotEmpty(result);
        var resultList = result.ToList();

        // Our discounts should be the first 3 in the result
        Assert.Equal(discount3.Id, resultList[0].Id);
        Assert.Equal(discount2.Id, resultList[1].Id);
        Assert.Equal(discount1.Id, resultList[2].Id);

        // Cleanup
        await subscriptionDiscountRepository.DeleteAsync(discount1);
        await subscriptionDiscountRepository.DeleteAsync(discount2);
        await subscriptionDiscountRepository.DeleteAsync(discount3);
    }

    [Theory, DatabaseData]
    public async Task SearchAsync_WithSkip_ReturnsCorrectPage(
        ISubscriptionDiscountRepository subscriptionDiscountRepository)
    {
        // Arrange - create several discounts with future timestamps (should be at top)
        var farFuture = new DateTime(2500, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var discounts = new List<SubscriptionDiscount>();
        for (int i = 0; i < 5; i++)
        {
            var discount = await subscriptionDiscountRepository.CreateAsync(
                CreateTestDiscount(
                    stripeCouponId: $"test-skip-{i}-{Guid.NewGuid()}",
                    percentOff: 10.00m + i,
                    name: $"Discount {i}",
                    creationDate: farFuture.AddSeconds(-i)));
            discounts.Add(discount);
        }

        // Act - get first page to find where our discounts are
        var allResults = await subscriptionDiscountRepository.SearchAsync(0, 100);
        var allResultsList = allResults.ToList();

        // Find the indices of our created discounts
        var indices = discounts.Select(d => allResultsList.FindIndex(r => r.Id == d.Id)).Where(i => i >= 0).OrderBy(i => i).ToList();

        // Act - skip the first 2 of OUR discounts, take 2
        var result = await subscriptionDiscountRepository.SearchAsync(indices[2], 2);

        // Assert
        var resultList = result.ToList();
        Assert.True(resultList.Count == 2);

        // Verify we got discounts[2] and discounts[3]
        Assert.Contains(resultList, d => d.Id == discounts[2].Id);
        Assert.Contains(resultList, d => d.Id == discounts[3].Id);

        // Cleanup
        foreach (var discount in discounts)
        {
            await subscriptionDiscountRepository.DeleteAsync(discount);
        }
    }

    [Theory, DatabaseData]
    public async Task SearchAsync_WithTake_LimitsResults(
        ISubscriptionDiscountRepository subscriptionDiscountRepository)
    {
        // Arrange - create 5 discounts
        var discounts = new List<SubscriptionDiscount>();
        for (int i = 0; i < 5; i++)
        {
            var discount = await subscriptionDiscountRepository.CreateAsync(
                CreateTestDiscount(
                    stripeCouponId: $"test-limit-{i}-{Guid.NewGuid()}",
                    percentOff: 10.00m,
                    name: $"Discount {i}",
                    creationDate: DateTime.UtcNow.AddMinutes(-i)));
            discounts.Add(discount);
        }

        // Act - get only 3 results
        var result = await subscriptionDiscountRepository.SearchAsync(0, 3);

        // Assert
        var resultList = result.ToList();
        Assert.True(resultList.Count == 3);

        // Cleanup
        foreach (var discount in discounts)
        {
            await subscriptionDiscountRepository.DeleteAsync(discount);
        }
    }
}
