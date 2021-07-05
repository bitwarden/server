using System;
using Bit.Core.Models.Table;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Data;
using Dapper;
using System.Linq;
using System.Collections.Generic;
using Bit.Core.Models.Table.Provider;
using Bit.Core.Settings;

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
    }
}
