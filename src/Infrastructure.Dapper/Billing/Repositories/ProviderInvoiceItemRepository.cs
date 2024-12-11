using System.Data;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Billing.Repositories;

public class ProviderInvoiceItemRepository(GlobalSettings globalSettings)
    : Repository<ProviderInvoiceItem, Guid>(
        globalSettings.SqlServer.ConnectionString,
        globalSettings.SqlServer.ReadOnlyConnectionString
    ),
        IProviderInvoiceItemRepository
{
    public async Task<ICollection<ProviderInvoiceItem>> GetByInvoiceId(string invoiceId)
    {
        var sqlConnection = new SqlConnection(ConnectionString);

        var results = await sqlConnection.QueryAsync<ProviderInvoiceItem>(
            "[dbo].[ProviderInvoiceItem_ReadByInvoiceId]",
            new { InvoiceId = invoiceId },
            commandType: CommandType.StoredProcedure
        );

        return results.ToArray();
    }

    public async Task<ICollection<ProviderInvoiceItem>> GetByProviderId(Guid providerId)
    {
        var sqlConnection = new SqlConnection(ConnectionString);

        var results = await sqlConnection.QueryAsync<ProviderInvoiceItem>(
            "[dbo].[ProviderInvoiceItem_ReadByProviderId]",
            new { ProviderId = providerId },
            commandType: CommandType.StoredProcedure
        );

        return results.ToArray();
    }
}
