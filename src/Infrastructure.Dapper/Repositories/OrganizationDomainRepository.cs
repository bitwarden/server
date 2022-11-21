using System.Data;
using System.Data.SqlClient;
using Bit.Core.Entities;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;

namespace Bit.Infrastructure.Dapper.Repositories;

public class OrganizationDomainRepository : Repository<OrganizationDomain, Guid>, IOrganizationDomainRepository
{
    public OrganizationDomainRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public OrganizationDomainRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<ICollection<OrganizationDomain>> GetClaimedDomainsByDomainNameAsync(string domainName)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationDomain>(
                $"[{Schema}].[OrganizationDomain_ReadByClaimedDomain]",
                new { DomainName = domainName },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ICollection<OrganizationDomain>> GetDomainsByOrganizationId(Guid orgId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<OrganizationDomain>(
                $"[{Schema}].[OrganizationDomain_ReadByOrganizationId]",
                new { OrganizationId = orgId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }
}
