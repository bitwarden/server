using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Models.Table.Provider;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Core.Repositories.SqlServer
{
    public class ProviderUserRepository : Repository<ProviderUser, Guid>, IProviderUserRepository
    {
        public ProviderUserRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
        { }

        public ProviderUserRepository(string connectionString, string readOnlyConnectionString)
            : base(connectionString, readOnlyConnectionString)
        { }

        public async Task<int> GetCountByProviderAsync(Guid providerId, string email, bool onlyRegisteredUsers)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                var result = await connection.ExecuteScalarAsync<int>(
                    "[dbo].[ProviderUser_ReadCountByProviderIdEmail]",
                    new { ProviderId = providerId, Email = email, OnlyUsers = onlyRegisteredUsers },
                    commandType: CommandType.StoredProcedure);

                return result;
            }
        }
        
        public async Task<ICollection<ProviderUser>> GetManyAsync(IEnumerable<Guid> Ids)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<ProviderUser>(
                    "[dbo].[ProviderUser_ReadByIds]",
                    new { Ids = Ids.ToGuidIdArrayTVP() },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }
    }
}
