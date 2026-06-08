using System.Data;
using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

#nullable enable

namespace Bit.Infrastructure.Dapper.Pam.Repositories;

public class LeaseRepository : Repository<Lease, Guid>, ILeaseRepository
{
    public LeaseRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public LeaseRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<Lease?> GetActiveByRequesterIdCipherIdAsync(Guid requesterId, Guid cipherId, DateTime now)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<Lease>(
            $"[{Schema}].[Lease_ReadActiveByRequesterIdCipherId]",
            new { RequesterId = requesterId, CipherId = cipherId, Now = now },
            commandType: CommandType.StoredProcedure);

        return results.FirstOrDefault();
    }

    public async Task<ICollection<Lease>> GetManyActiveByRequesterIdAsync(Guid requesterId, DateTime now)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<Lease>(
            $"[{Schema}].[Lease_ReadManyActiveByRequesterId]",
            new { RequesterId = requesterId, Now = now },
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }

    public async Task CreateAutoApprovedAsync(LeaseRequest request, LeaseDecision decision, Lease lease, DateTime now)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            $"[{Schema}].[Lease_CreateAutoApproved]",
            new
            {
                LeaseRequestId = request.Id,
                LeaseId = lease.Id,
                LeaseDecisionId = decision.Id,
                request.OrganizationId,
                request.CollectionId,
                request.CipherId,
                request.RequesterId,
                request.NotBefore,
                request.NotAfter,
                request.Reason,
                decision.PolicyKind,
                Now = now,
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task RevokeAsync(Lease lease, LeaseDecision auditDecision, DateTime now)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            $"[{Schema}].[Lease_Revoke]",
            new
            {
                LeaseId = lease.Id,
                LeaseRequestId = lease.LeaseRequestId,
                RevokedBy = auditDecision.ApproverId,
                LeaseDecisionId = auditDecision.Id,
                Reason = auditDecision.Comment,
                Now = now,
            },
            commandType: CommandType.StoredProcedure);
    }
}
