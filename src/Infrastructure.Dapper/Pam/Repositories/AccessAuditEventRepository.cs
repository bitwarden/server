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

    public async Task<ICollection<AccessAuditEvent>> GetManyByCollectionIdsAsync(
        IEnumerable<Guid> collectionIds, DateTime since, DateTime now)
    {
        var ids = collectionIds.ToList();
        if (ids.Count == 0)
        {
            return new List<AccessAuditEvent>();
        }

        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<AccessAuditEvent>(
            "[dbo].[AccessAuditEvent_ReadManyByCollectionIds]",
            new { CollectionIds = ids.ToGuidIdArrayTVP(), Since = since, Now = now },
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }
}
