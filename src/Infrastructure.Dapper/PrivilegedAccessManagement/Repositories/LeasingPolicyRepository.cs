using System.Data;
using Bit.Core.PrivilegedAccessManagement.Entities;
using Bit.Core.PrivilegedAccessManagement.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

#nullable enable

namespace Bit.Infrastructure.Dapper.PrivilegedAccessManagement.Repositories;

public class LeasingPolicyRepository : Repository<LeasingPolicy, Guid>, ILeasingPolicyRepository
{
    public LeasingPolicyRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public LeasingPolicyRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<ICollection<LeasingPolicy>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<LeasingPolicy>(
            $"[{Schema}].[LeasingPolicy_ReadByOrganizationId]",
            new { OrganizationId = organizationId },
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }
}
