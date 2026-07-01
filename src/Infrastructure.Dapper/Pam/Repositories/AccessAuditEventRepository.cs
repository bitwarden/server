using System.Data;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

#nullable enable

namespace Bit.Infrastructure.Dapper.Pam.Repositories;

public class AccessAuditEventRepository : BaseRepository, IAccessAuditEventRepository
{
    public AccessAuditEventRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public AccessAuditEventRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<ICollection<AccessAuditEvent>> GetManyByOrganizationIdAsync(
        Guid organizationId, DateTime since, DateTime now)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<AccessAuditEvent>(
            "[dbo].[AccessAuditEvent_ReadManyByOrganizationId]",
            new { OrganizationId = organizationId, Since = since, Now = now },
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }
}
