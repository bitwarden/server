using System.Data;
using System.Text.Json;
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
                auditEvent.OccurredAt,
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

    public async Task<ICollection<AccessAuditEvent>> GetPageByOrganizationIdAsync(
        Guid organizationId, AccessAuditTrailFilter filter)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@OrganizationId", organizationId, DbType.Guid);
        parameters.Add("@PageSize", filter.PageSize, DbType.Int32);
        // Explicitly use DbType.DateTime2 for proper precision.
        // ref: https://github.com/StackExchange/Dapper/issues/229
        parameters.Add("@StartDate", filter.Since, DbType.DateTime2, null, 7);
        parameters.Add("@EndDate", filter.Until, DbType.DateTime2, null, 7);
        parameters.Add("@BeforeDate", filter.BeforeOccurredAt, DbType.DateTime2, null, 7);
        parameters.Add("@BeforeId", filter.BeforeId, DbType.Guid);
        parameters.Add("@Kinds", JsonList(filter.Kinds.Select(kind => (byte)kind)), DbType.String);
        parameters.Add("@ActorIds", JsonList(filter.ActorIds), DbType.String);
        parameters.Add("@IncludeAutomatedActor", filter.IncludeAutomatedActor, DbType.Boolean);
        parameters.Add("@RequesterIds", JsonList(filter.RequesterIds), DbType.String);
        parameters.Add("@CipherIds", JsonList(filter.CipherIds), DbType.String);
        parameters.Add("@RuleIds", JsonList(filter.RuleIds), DbType.String);

        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<AccessAuditEvent>(
            "[dbo].[AccessAuditEvent_ReadPageByOrganizationId]",
            parameters,
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }

    public async Task<ICollection<AccessAuditItem>> GetItemsByOrganizationIdAsync(
        Guid organizationId, DateTime since, DateTime until)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@OrganizationId", organizationId, DbType.Guid);
        // Explicitly use DbType.DateTime2 for proper precision.
        // ref: https://github.com/StackExchange/Dapper/issues/229
        parameters.Add("@StartDate", since, DbType.DateTime2, null, 7);
        parameters.Add("@EndDate", until, DbType.DateTime2, null, 7);

        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<AccessAuditItem>(
            "[dbo].[AccessAuditEvent_ReadItemsByOrganizationId]",
            parameters,
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }

    /// <summary>
    /// A selection as the JSON array the procedure's OPENJSON reads, or null when nothing is selected — which is how
    /// the procedure is told the dimension is unfiltered, and is not the same as an empty array (which would match
    /// nothing).
    /// </summary>
    private static string? JsonList<T>(IEnumerable<T> values)
    {
        var selected = values.ToList();
        return selected.Count == 0 ? null : JsonSerializer.Serialize(selected);
    }
}
