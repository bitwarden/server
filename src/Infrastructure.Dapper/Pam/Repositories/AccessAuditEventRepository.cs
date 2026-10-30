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
        var parameters = new DynamicParameters(new
        {
            Id = CombGuid.Generate(),
            auditEvent.CorrelationId,
            auditEvent.OrganizationId,
            Kind = (byte)auditEvent.Kind,
            Phase = (byte)auditEvent.Phase,
            auditEvent.ActorId,
            auditEvent.RequesterId,
            auditEvent.CollectionId,
            auditEvent.CipherId,
            auditEvent.AccessRequestId,
            auditEvent.AccessLeaseId,
            auditEvent.AccessRuleId,
            auditEvent.RuleName,
            auditEvent.Detail,
            auditEvent.TargetSystemId,
            auditEvent.TargetSystemName,
            auditEvent.AccessConnectorId,
            auditEvent.AccessConnectorName,
            auditEvent.RotationConfigId,
            auditEvent.RotationJobId,
            RotationSource = (byte?)auditEvent.RotationSource,
            SyncState = (byte?)auditEvent.SyncState,
        });

        // DATETIME2(7), since Dapper's default DATETIME rounds to about 3ms and the paging cursor needs the column's
        // full precision.
        parameters.Add("@OccurredDate", auditEvent.OccurredDate, DbType.DateTime2, null, 7);
        parameters.Add("@LeaseNotBefore", auditEvent.LeaseNotBefore, DbType.DateTime2, null, 7);
        parameters.Add("@LeaseNotAfter", auditEvent.LeaseNotAfter, DbType.DateTime2, null, 7);

        await using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            "[dbo].[AccessAuditEvent_Create]",
            parameters,
            commandType: CommandType.StoredProcedure);
    }

    public async Task<ICollection<AccessAuditEvent>> GetPageByOrganizationIdAsync(
        Guid organizationId, AccessAuditTrailFilter filter)
    {
        // DATETIME2(7) as on the write: the bounds and the cursor are stable page boundaries only at full precision.
        // ref: https://github.com/StackExchange/Dapper/issues/229
        var parameters = new DynamicParameters();
        parameters.Add("@OrganizationId", organizationId, DbType.Guid);
        parameters.Add("@StartDate", filter.Since, DbType.DateTime2, null, 7);
        parameters.Add("@EndDate", filter.Until, DbType.DateTime2, null, 7);
        parameters.Add("@PageSize", filter.PageSize, DbType.Int32);
        parameters.Add("@BeforeDate", filter.Before?.OccurredDate, DbType.DateTime2, null, 7);
        parameters.Add("@BeforeId", filter.Before?.Id, DbType.Guid);
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
    /// Null when nothing is selected, which the procedure reads as unfiltered; an empty array would match nothing.
    /// </summary>
    private static string? JsonList<T>(IEnumerable<T> values)
    {
        var selected = values.ToList();
        return selected.Count == 0 ? null : JsonSerializer.Serialize(selected);
    }
}
