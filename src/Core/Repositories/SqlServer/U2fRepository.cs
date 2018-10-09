using System;
using System.Linq;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Bit.Core.Models.Table;
using System.Data;
using Dapper;

namespace Bit.Core.Repositories.SqlServer
{
    public class U2fRepository : Repository<U2f, int>, IU2fRepository
    {
        public U2fRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
        { }

        public U2fRepository(string connectionString, string readOnlyConnectionString)
            : base(connectionString, readOnlyConnectionString)
        { }

        public async Task<ICollection<U2f>> GetManyByUserIdAsync(Guid userId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<U2f>(
                    $"[{Schema}].[U2f_ReadByUserId]",
                    new { UserId = userId },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task DeleteManyByUserIdAsync(Guid userId)
        {
            using(var connection = new SqlConnection(ConnectionString))
            {
                await connection.ExecuteAsync(
                    $"[{Schema}].[U2f_DeleteByUserId]",
                    new { UserId = userId },
                    commandType: CommandType.StoredProcedure);
            }
        }

        public override Task<U2f> GetByIdAsync(int id)
        {
            throw new NotSupportedException();
        }

        public override Task ReplaceAsync(U2f obj)
        {
            throw new NotSupportedException();
        }

        public override Task UpsertAsync(U2f obj)
        {
            throw new NotSupportedException();
        }

        public override Task DeleteAsync(U2f obj)
        {
            throw new NotSupportedException();
        }
    }
}
