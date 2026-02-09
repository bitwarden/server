#nullable enable

using Bit.Core.Billing.Subscriptions.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.Billing.Subscriptions.Repositories;

public interface ISubscriptionDiscountRepository : IRepository<SubscriptionDiscount, Guid>
{
    /// <summary>
    /// Retrieves all active subscription discounts that are currently within their valid date range.
    /// A discount is considered active if the current UTC date falls between StartDate (inclusive) and EndDate (inclusive).
    /// </summary>
    /// <returns>A collection of active subscription discounts.</returns>
    Task<ICollection<SubscriptionDiscount>> GetActiveDiscountsAsync();

    /// <summary>
    /// Retrieves a subscription discount by its Stripe coupon ID.
    /// </summary>
    /// <param name="stripeCouponId">The Stripe coupon ID to search for.</param>
    /// <returns>The subscription discount if found; otherwise, null.</returns>
    Task<SubscriptionDiscount?> GetByStripeCouponIdAsync(string stripeCouponId);

    /// <summary>
    /// Searches subscription discounts with pagination support.
    /// Returns discounts ordered by creation date descending (newest first).
    /// </summary>
    /// <param name="skip">Number of records to skip (for pagination).</param>
    /// <param name="take">Number of records to take (page size).</param>
    /// <returns>A collection of subscription discounts for the requested page.</returns>
    Task<ICollection<SubscriptionDiscount>> SearchAsync(int skip, int take);
}
