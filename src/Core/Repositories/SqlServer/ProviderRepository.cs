using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table.Provider;
using Bit.Core.Settings;
using Dapper;

namespace Bit.Core.Repositories.SqlServer
{
    public class ProviderRepository : Repository<Provider, Guid>, IProviderRepository
    {
        public ProviderRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
        { }

        public ProviderRepository(string connectionString, string readOnlyConnectionString)
            : base(connectionString, readOnlyConnectionString)
        { }

        public async Task<ICollection<Provider>> SearchAsync(string name, string userEmail, int skip, int take)
        {
            using (var connection = new SqlConnection(ReadOnlyConnectionString))
            {
                var results = await connection.QueryAsync<Provider>(
                    "[dbo].[Provider_Search]",
                    new { Name = name, UserEmail = userEmail, Skip = skip, Take = take },
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 120);

                return results.ToList();
            }
        }

        public async Task<ICollection<ProviderAbility>> GetManyAbilitiesAsync()
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<ProviderAbility>(
                    "[dbo].[Provider_ReadAbilities]",
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }
    }
}
