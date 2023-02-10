using System.Data;
using System.Text.Json;
using Bit.Core.Entities.Provider;
using Bit.Core.Models.Data;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Repositories;

public class ProviderOrganizationRepository : Repository<ProviderOrganization, Guid>, IProviderOrganizationRepository
{
    public ProviderOrganizationRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public ProviderOrganizationRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task CreateWithManyOrganizations(ProviderOrganization providerOrganization, IEnumerable<Guid> organizationIds)
    {
        var objWithOrganizationIds = JsonSerializer.Deserialize<ProviderOrganizationWithOrganizations>(
            JsonSerializer.Serialize(providerOrganization));
        objWithOrganizationIds.OrganizationIds = organizationIds.ToGuidIdArrayTVP();
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.ExecuteAsync(
                $"[{Schema}].[ProviderOrganization_CreateWithManyOrganizations]",
                objWithOrganizationIds,
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task<ICollection<ProviderOrganizationUnassignedOrganizationDetails>> SearchAsync(string name, string ownerEmail, int skip, int take)
    {
        using (var connection = new SqlConnection(ReadOnlyConnectionString))
        {
            var results = await connection.QueryAsync<ProviderOrganizationUnassignedOrganizationDetails>(
                "[dbo].[ProviderOrganizationUnassignedOrganizationDetails_Search]",
                new { Name = name, OwnerEmail = ownerEmail, Skip = skip, Take = take },
                commandType: CommandType.StoredProcedure,
                commandTimeout: 120);

            return results.ToList();
        }
    }

    public async Task<ICollection<ProviderOrganizationOrganizationDetails>> GetManyDetailsByProviderAsync(Guid providerId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<ProviderOrganizationOrganizationDetails>(
                "[dbo].[ProviderOrganizationOrganizationDetails_ReadByProviderId]",
                new { ProviderId = providerId },
                commandType: CommandType.StoredProcedure);

            return results.ToList();
        }
    }

    public async Task<ProviderOrganization> GetByOrganizationId(Guid organizationId)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            var results = await connection.QueryAsync<ProviderOrganization>(
                "[dbo].[ProviderOrganization_ReadByOrganizationId]",
                new { OrganizationId = organizationId },
                commandType: CommandType.StoredProcedure);

            return results.SingleOrDefault();
        }
    }

    public class ProviderOrganizationWithOrganizations : ProviderOrganization
    {
        public DataTable OrganizationIds { get; set; }
    }
}
