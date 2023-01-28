using System.Data;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Repositories;

public class TransactionRepository : Repository<Transaction, Guid>, ITransactionRepository
{
    public TransactionRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public TransactionRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<ICollection<Transaction>> GetManyByUserIdAsync(Guid userId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Transaction>(
                $"[{Schema}].[Transaction_ReadByUserId]",
                new { UserId = userId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ICollection<Transaction>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Transaction>(
                $"[{Schema}].[Transaction_ReadByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<Transaction> GetByGatewayIdAsync(GatewayType gatewayType, string gatewayId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Transaction>(
                $"[{Schema}].[Transaction_ReadByGatewayId]",
                new { Gateway = gatewayType, GatewayId = gatewayId },
                commandType: CommandType.StoredProcedure);

            return results.SingleOrDefault();
        }
    }
}
