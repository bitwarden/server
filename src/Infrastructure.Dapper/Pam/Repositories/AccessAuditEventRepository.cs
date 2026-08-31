using System.Data;
using Bit.Core.Settings;
using Bit.Core.Utilities;
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

    public async Task CreateAsync(AccessAuditEventData auditEvent)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            "[dbo].[AccessAuditEvent_Create]",
            new
            {
                Id = CombGuid.Generate(),
                auditEvent.CorrelationId,
                auditEvent.OrganizationId,
                Kind = (byte)auditEvent.Kind,
                Phase = (byte)auditEvent.Phase,
                auditEvent.OccurredDate,
                auditEvent.ActorId,
                auditEvent.RequesterId,
                auditEvent.CollectionId,
                auditEvent.CipherId,
                auditEvent.AccessRequestId,
                auditEvent.AccessLeaseId,
                auditEvent.AccessRuleId,
                auditEvent.RuleName,
                auditEvent.Detail,
                auditEvent.LeaseNotBefore,
                auditEvent.LeaseNotAfter,
                auditEvent.TargetSystemId,
                auditEvent.TargetSystemName,
                auditEvent.DaemonId,
                auditEvent.DaemonName,
                auditEvent.RotationConfigId,
                auditEvent.RotationJobId,
                RotationSource = (byte?)auditEvent.RotationSource,
                SyncState = (byte?)auditEvent.SyncState,
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<ICollection<AccessAuditEvent>> GetManyByOrganizationIdAsync(
        Guid organizationId, DateTime since, AccessAuditEventCursor? before, int take)
    {
        // The cursor is only a stable page boundary if it round-trips at the column's full precision, so the two
        // datetimes are declared as DATETIME2(7) rather than left to Dapper's default mapping, which truncates.
        // ref: https://github.com/StackExchange/Dapper/issues/229
        var parameters = new DynamicParameters();
        parameters.Add("@OrganizationId", organizationId, DbType.Guid);
        parameters.Add("@Since", since, DbType.DateTime2, null, 7);
        parameters.Add("@BeforeOccurredDate", before?.OccurredDate, DbType.DateTime2, null, 7);
        parameters.Add("@BeforeId", before?.Id, DbType.Guid);
        parameters.Add("@Take", take, DbType.Int32);

        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<AccessAuditEvent>(
            "[dbo].[AccessAuditEvent_ReadManyByOrganizationId]",
            parameters,
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }
}
