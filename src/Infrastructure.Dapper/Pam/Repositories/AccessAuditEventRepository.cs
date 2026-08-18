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
                Id = CoreHelpers.GenerateComb(),
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
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<ICollection<AccessAuditEvent>> GetManyByOrganizationIdAsync(
        Guid organizationId, DateTime since)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<AccessAuditEvent>(
            "[dbo].[AccessAuditEvent_ReadManyByOrganizationId]",
            new { OrganizationId = organizationId, Since = since },
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }
}
