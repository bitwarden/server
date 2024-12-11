using System.Data;
using Bit.Core.Billing.Entities;
using Bit.Core.Billing.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Billing.Repositories;

public class ProviderPlanRepository(GlobalSettings globalSettings)
    : Repository<ProviderPlan, Guid>(
        globalSettings.SqlServer.ConnectionString,
        globalSettings.SqlServer.ReadOnlyConnectionString
    ),
        IProviderPlanRepository
{
    public async Task<ICollection<ProviderPlan>> GetByProviderId(Guid providerId)
    {
        var sqlConnection = new SqlConnection(ConnectionString);

        var results = await sqlConnection.QueryAsync<ProviderPlan>(
            "[dbo].[ProviderPlan_ReadByProviderId]",
            new { ProviderId = providerId },
            commandType: CommandType.StoredProcedure
        );

        return results.ToArray();
    }
}
