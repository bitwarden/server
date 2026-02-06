using System.Data;
using Bit.Core.Billing.Subscriptions.Entities;
using Bit.Core.Billing.Subscriptions.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Billing.Repositories;

public class SubscriptionDiscountRepository(
    GlobalSettings globalSettings)
    : Repository<SubscriptionDiscount, Guid>(
        globalSettings.SqlServer.ConnectionString,
        globalSettings.SqlServer.ReadOnlyConnectionString), ISubscriptionDiscountRepository
{
    public async Task<ICollection<SubscriptionDiscount>> GetActiveDiscountsAsync()
    {
        using var sqlConnection = new SqlConnection(ReadOnlyConnectionString);

        var results = await sqlConnection.QueryAsync<SubscriptionDiscount>(
            "[dbo].[SubscriptionDiscount_ReadActive]",
            commandType: CommandType.StoredProcedure);

        return results.ToArray();
    }

    public async Task<SubscriptionDiscount?> GetByStripeCouponIdAsync(string stripeCouponId)
    {
        using var sqlConnection = new SqlConnection(ReadOnlyConnectionString);

        var result = await sqlConnection.QueryFirstOrDefaultAsync<SubscriptionDiscount>(
            "[dbo].[SubscriptionDiscount_ReadByStripeCouponId]",
            new { StripeCouponId = stripeCouponId },
            commandType: CommandType.StoredProcedure);

        return result;
    }
}
