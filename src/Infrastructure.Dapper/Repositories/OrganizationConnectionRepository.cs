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
using Dapper;

namespace Bit.Infrastructure.Dapper.Repositories
{
    public class OrganizationConnectionRepository : Repository<OrganizationConnection, Guid>, IOrganizationConnectionRepository
    {
        public OrganizationConnectionRepository(GlobalSettings globalSettings)
            : base(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
        { }

        public async Task<ICollection<OrganizationConnection>> GetEnabledByOrganizationIdTypeAsync(Guid organizationId, OrganizationConnectionType type)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                var results = await connection.QueryAsync<OrganizationConnection>(
                    $"[{Schema}].[OrganizationConnection_ReadEnabledByOrganizationIdType]",
                    new
                    {
                        OrganizationId = organizationId,
                        Type = type
                    },
                    commandType: CommandType.StoredProcedure);

                return results.ToList();
            }
        }
    }
}
