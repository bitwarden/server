using System.Data;
using Bit.Core.AdminConsole.Entities.Provider;
using Bit.Core.AdminConsole.Models.Data.Provider;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

#nullable enable

namespace Bit.Infrastructure.Dapper.AdminConsole.Repositories;

public class ProviderRepository : Repository<Provider, Guid>, IProviderRepository
{
    public ProviderRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public ProviderRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<Provider?> GetByOrganizationIdAsync(Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<Provider>(
                "[dbo].[Provider_ReadByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results.FirstOrDefault();
        }
    }

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
