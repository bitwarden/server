using System.Data;
using Bit.Core.Entities;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;
using Microsoft.Data.SqlClient;

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

    public async Task<ICollection<OrganizationDomain>> GetDomainsByOrganizationIdAsync(Guid orgId)
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

    public async Task<ICollection<OrganizationDomain>> GetManyByNextRunDateAsync(DateTime date)
    {
        using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<OrganizationDomain>(
            $"[{Schema}].[OrganizationDomain_ReadByNextRunDate]",
            new { Date = date }, commandType: CommandType.StoredProcedure
        );

        return results.ToList();
    }

    public async Task<OrganizationDomainSsoDetailsData> GetOrganizationDomainSsoDetailsAsync(string email)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection
                .QueryAsync<OrganizationDomainSsoDetailsData>(
                    $"[{Schema}].[OrganizationDomainSsoDetails_ReadByEmail]",
                    new { Email = email },
                    commandType: CommandType.StoredProcedure);

            return results.SingleOrDefault();
        }
    }

    public async Task<OrganizationDomain> GetDomainByOrgIdAndDomainNameAsync(Guid orgId, string domainName)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection
                .QueryAsync<OrganizationDomain>(
                    $"[{Schema}].[OrganizationDomain_ReadDomainByOrgIdAndDomainName]",
                    new { OrganizationId = orgId, DomainName = domainName },
                    commandType: CommandType.StoredProcedure);

            return results.SingleOrDefault();
        }
    }

    public async Task<ICollection<OrganizationDomain>> GetExpiredOrganizationDomainsAsync()
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection
                .QueryAsync<OrganizationDomain>(
                    $"[{Schema}].[OrganizationDomain_ReadIfExpired]",
                    null,
                    commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<bool> DeleteExpiredAsync(int expirationPeriod)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            return await connection.ExecuteAsync(
                $"[{Schema}].[OrganizationDomain_DeleteIfExpired]",
                new { ExpirationPeriod = expirationPeriod },
                commandType: CommandType.StoredProcedure) > 0;
        }
    }
}
