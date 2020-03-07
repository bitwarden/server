using System;
using Bit.Core.Models.Table;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using Bit.Core.Enums;

namespace Bit.Core.Repositories.SqlServer
{
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
            using(var connection = new SqlConnection(ConnectionString))
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
            using(var connection = new SqlConnection(ConnectionString))
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
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<Transaction>(
                    $"[{Schema}].[Transaction_ReadByGatewayId]",
                    new { Gateway = gatewayType, GatewayId = gatewayId },
                    commandType: CommandType.StoredProcedure);

                return results.SingleOrDefault();
            }
        }
    }
}
