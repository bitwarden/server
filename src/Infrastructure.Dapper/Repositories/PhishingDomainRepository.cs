using System.Data;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Repositories;

public class PhishingDomainRepository : IPhishingDomainRepository
{
    private readonly string _connectionString;

    public PhishingDomainRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString)
    { }

    public PhishingDomainRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<ICollection<string>> GetActivePhishingDomainsAsync()
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            var results = await connection.QueryAsync<string>(
                "[dbo].[PhishingDomain_ReadAll]",
                commandType: CommandType.StoredProcedure);
            return results.AsList();
        }
    }

    public async Task UpdatePhishingDomainsAsync(IEnumerable<string> domains)
    {
        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.ExecuteAsync(
                "[dbo].[PhishingDomain_DeleteAll]",
                commandType: CommandType.StoredProcedure);

            foreach (var domain in domains)
            {
                await connection.ExecuteAsync(
                    "[dbo].[PhishingDomain_Create]",
                    new
                    {
                        Id = Guid.NewGuid(),
                        Domain = domain,
                        CreationDate = DateTime.UtcNow,
                        RevisionDate = DateTime.UtcNow
                    },
                    commandType: CommandType.StoredProcedure);
            }
        }
    }
}
