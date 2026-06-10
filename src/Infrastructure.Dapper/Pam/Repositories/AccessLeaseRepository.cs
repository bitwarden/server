using System.Data;
using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

#nullable enable

namespace Bit.Infrastructure.Dapper.Pam.Repositories;

public class AccessLeaseRepository : Repository<AccessLease, Guid>, IAccessLeaseRepository
{
    public AccessLeaseRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public AccessLeaseRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<AccessLease?> GetByAccessRequestIdAsync(Guid accessRequestId)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<AccessLease>(
            $"[{Schema}].[AccessLease_ReadByAccessRequestId]",
            new { AccessRequestId = accessRequestId },
            commandType: CommandType.StoredProcedure);

        return results.FirstOrDefault();
    }

    public async Task<AccessLease?> GetActiveByRequesterIdCipherIdAsync(Guid requesterId, Guid cipherId, DateTime now)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<AccessLease>(
            $"[{Schema}].[AccessLease_ReadActiveByRequesterIdCipherId]",
            new { RequesterId = requesterId, CipherId = cipherId, Now = now },
            commandType: CommandType.StoredProcedure);

        return results.FirstOrDefault();
    }

    public async Task<ICollection<AccessLease>> GetManyActiveByRequesterIdAsync(Guid requesterId, DateTime now)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<AccessLease>(
            $"[{Schema}].[AccessLease_ReadManyActiveByRequesterId]",
            new { RequesterId = requesterId, Now = now },
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }

    public async Task CreateAutoApprovedAsync(AccessRequest request, AccessDecision decision, AccessLease lease, DateTime now)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            $"[{Schema}].[AccessLease_CreateAutoApproved]",
            new
            {
                AccessRequestId = request.Id,
                AccessLeaseId = lease.Id,
                AccessDecisionId = decision.Id,
                request.OrganizationId,
                request.CollectionId,
                request.CipherId,
                request.RequesterId,
                request.NotBefore,
                request.NotAfter,
                request.Reason,
                decision.ConditionKind,
                Now = now,
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<bool> CreateFromApprovedRequestAsync(AccessLease lease, DateTime now)
    {
        await using var connection = new SqlConnection(ConnectionString);
        try
        {
            var rows = await connection.ExecuteScalarAsync<int>(
                $"[{Schema}].[AccessLease_CreateFromApprovedRequest]",
                new
                {
                    AccessLeaseId = lease.Id,
                    lease.AccessRequestId,
                    lease.RequesterId,
                    Now = now,
                },
                commandType: CommandType.StoredProcedure);

            return rows == 1;
        }
        catch (SqlException e) when (e.Number is 2601 or 2627)
        {
            // Unique-index backstop ([IX_AccessLease_AccessRequestId]): a concurrent activation won the race after
            // our NOT EXISTS guard passed. Same outcome as the guard catching it — the caller re-reads the winner.
            return false;
        }
    }

    public async Task RevokeAsync(AccessLease lease, AccessDecision auditDecision, DateTime now)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            $"[{Schema}].[AccessLease_Revoke]",
            new
            {
                AccessLeaseId = lease.Id,
                AccessRequestId = lease.AccessRequestId,
                RevokedBy = auditDecision.ApproverId,
                AccessDecisionId = auditDecision.Id,
                Reason = auditDecision.Comment,
                Now = now,
            },
            commandType: CommandType.StoredProcedure);
    }
}
