using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Dapper;

namespace Bit.Infrastructure.Dapper.Repositories
{
    public class OrganizationApiKeyRepository : BaseRepository, IOrganizationApiKeyRepository
    {
        public OrganizationApiKeyRepository(GlobalSettings globalSettings)
            : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
        {

        }

        public OrganizationApiKeyRepository(string connectionString, string readOnlyConnectionString)
            : base(connectionString, readOnlyConnectionString)
        { }

        public async Task<OrganizationApiKey> GetByOrganizationIdTypeAsync(Guid organizationId, OrganizationApiKeyType type)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                return await connection.QuerySingleOrDefaultAsync<OrganizationApiKey>(
                    "[dbo].[OrganizationApiKey_ReadByOrganizationIdType]",
                    new
                    {
                        OrganizationId = organizationId,
                        Type = type,
                    },
                    commandType: CommandType.StoredProcedure);
            }
        }

        public async Task<ICollection<OrganizationApiKey>> GetByOrganizationIdAsync(Guid organizationId)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<OrganizationApiKey>(
                    "[dbo].[OrganizationApiKey_ReadByOrganizationId]",
                    new
                    {
                        OrganizationId = organizationId,
                    },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }

        public async Task CreateAsync(OrganizationApiKey organizationApiKey)
        {
            organizationApiKey.Id = CoreHelpers.GenerateComb();

            using (var connection = new SqlConnection(ConnectionString))
            {
                await connection.ExecuteAsync(
                    "[dbo].[OrganizationApiKey_Create]",
                    organizationApiKey,
                    commandType: CommandType.StoredProcedure);
            }
        }

        public async Task UpdateAsync(OrganizationApiKey organizationApiKey)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                await connection.ExecuteAsync(
                    "[dbo].[OrganizationApiKey_Update]",
                    new 
                    { 
                        organizationApiKey.Id,
                        organizationApiKey.ApiKey,
                        organizationApiKey.RevisionDate,
                    },
                    commandType: CommandType.StoredProcedure);
            }
        }
    }
}
