using AutoMapper;
using Bit.Core.Billing.Subscriptions.Entities;
using Bit.Core.Billing.Subscriptions.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EFSubscriptionDiscount = Bit.Infrastructure.EntityFramework.Billing.Models.SubscriptionDiscount;

namespace Bit.Infrastructure.EntityFramework.Billing.Repositories;

public class SubscriptionDiscountRepository(
    IMapper mapper,
    IServiceScopeFactory serviceScopeFactory)
    : Repository<SubscriptionDiscount, EFSubscriptionDiscount, Guid>(
        serviceScopeFactory,
        mapper,
        context => context.SubscriptionDiscounts), ISubscriptionDiscountRepository
{
    public async Task<ICollection<SubscriptionDiscount>> GetActiveDiscountsAsync()
    {
        using var serviceScope = ServiceScopeFactory.CreateScope();

        var databaseContext = GetDatabaseContext(serviceScope);

        var query =
            from subscriptionDiscount in databaseContext.SubscriptionDiscounts
            where subscriptionDiscount.StartDate <= DateTime.UtcNow
                && subscriptionDiscount.EndDate >= DateTime.UtcNow
            select subscriptionDiscount;

        var results = await query.ToArrayAsync();

        return Mapper.Map<List<SubscriptionDiscount>>(results);
    }

    public async Task<SubscriptionDiscount?> GetByStripeCouponIdAsync(string stripeCouponId)
    {
        using var serviceScope = ServiceScopeFactory.CreateScope();

        var databaseContext = GetDatabaseContext(serviceScope);

        var query =
            from subscriptionDiscount in databaseContext.SubscriptionDiscounts
            where subscriptionDiscount.StripeCouponId == stripeCouponId
            select subscriptionDiscount;

        var result = await query.FirstOrDefaultAsync();

        return result == null ? null : Mapper.Map<SubscriptionDiscount>(result);
    }
}
