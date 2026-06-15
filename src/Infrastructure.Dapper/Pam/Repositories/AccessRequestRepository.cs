using System.Data;
using Bit.Core.Pam.Entities;
using Bit.Core.Pam.Enums;
using Bit.Core.Pam.Models;
using Bit.Core.Pam.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

#nullable enable

namespace Bit.Infrastructure.Dapper.Pam.Repositories;

public class AccessRequestRepository : Repository<AccessRequest, Guid>, IAccessRequestRepository
{
    public AccessRequestRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public AccessRequestRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task CreateAutoApprovedAsync(AccessRequest request, AccessDecision decision)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            $"[{Schema}].[AccessRequest_CreateAutoApproved]",
            new
            {
                AccessRequestId = request.Id,
                AccessDecisionId = decision.Id,
                request.OrganizationId,
                request.CollectionId,
                request.CipherId,
                request.RequesterId,
                request.NotBefore,
                request.NotAfter,
                request.Reason,
                decision.ConditionKind,
                CreationDate = request.CreationDate,
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<AccessRequest?> GetActivePendingByRequesterIdCipherIdAsync(Guid requesterId, Guid cipherId)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<AccessRequest>(
            $"[{Schema}].[AccessRequest_ReadActivePendingByRequesterIdCipherId]",
            new { RequesterId = requesterId, CipherId = cipherId },
            commandType: CommandType.StoredProcedure);

        return results.FirstOrDefault();
    }

    public async Task<AccessRequest?> GetActiveApprovedByRequesterIdCipherIdAsync(Guid requesterId, Guid cipherId, DateTime now)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<AccessRequest>(
            $"[{Schema}].[AccessRequest_ReadActiveApprovedByRequesterIdCipherId]",
            new { RequesterId = requesterId, CipherId = cipherId, Now = now },
            commandType: CommandType.StoredProcedure);

        return results.FirstOrDefault();
    }

    public async Task<ICollection<AccessRequestDetails>> GetManyByRequesterIdAsync(Guid requesterId)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<AccessRequestDetails>(
            $"[{Schema}].[AccessRequest_ReadManyByRequesterId]",
            new { RequesterId = requesterId },
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }

    public async Task<ICollection<AccessRequestDetails>> GetManyInboxPendingByCollectionIdsAsync(IEnumerable<Guid> collectionIds)
    {
        var ids = collectionIds.ToList();
        if (ids.Count == 0)
        {
            return new List<AccessRequestDetails>();
        }

        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<AccessRequestDetails>(
            $"[{Schema}].[AccessRequest_ReadInboxPendingByCollectionIds]",
            new { CollectionIds = ids.ToGuidIdArrayTVP() },
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }

    public async Task<ICollection<AccessRequestDetails>> GetManyInboxHistoryByCollectionIdsAsync(IEnumerable<Guid> collectionIds, DateTime since)
    {
        var ids = collectionIds.ToList();
        if (ids.Count == 0)
        {
            return new List<AccessRequestDetails>();
        }

        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<AccessRequestDetails>(
            $"[{Schema}].[AccessRequest_ReadInboxHistoryByCollectionIds]",
            new { CollectionIds = ids.ToGuidIdArrayTVP(), Since = since },
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }

    public async Task ResolveWithDecisionAsync(AccessRequest request, AccessDecision decision, AccessRequestStatus status, DateTime now)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            $"[{Schema}].[AccessRequest_ResolveWithDecision]",
            new
            {
                AccessRequestId = request.Id,
                Status = status,
                AccessDecisionId = decision.Id,
                ApproverId = decision.ApproverId,
                Verdict = decision.Verdict,
                decision.Comment,
                Now = now,
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task CancelAsync(Guid id, DateTime now)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            $"[{Schema}].[AccessRequest_Cancel]",
            new { AccessRequestId = id, Now = now },
            commandType: CommandType.StoredProcedure);
    }

    public async Task CancelWithDecisionAsync(AccessRequest request, AccessDecision decision, DateTime now)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            $"[{Schema}].[AccessRequest_CancelWithDecision]",
            new
            {
                AccessRequestId = request.Id,
                AccessDecisionId = decision.Id,
                ApproverId = decision.ApproverId,
                Verdict = decision.Verdict,
                decision.Comment,
                Now = now,
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<int> CountExtensionsByLeaseIdAsync(Guid leaseId)
    {
        await using var connection = new SqlConnection(ConnectionString);
        return await connection.ExecuteScalarAsync<int>(
            $"[{Schema}].[AccessRequest_CountExtensionsByLeaseId]",
            new { LeaseId = leaseId },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<AccessLeaseExtendOutcome> CreateApprovedExtensionAsync(AccessRequest request,
        AccessDecision decision, DateTime now)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var result = await connection.ExecuteScalarAsync<int>(
            $"[{Schema}].[AccessRequest_CreateApprovedExtension]",
            new
            {
                AccessRequestId = request.Id,
                AccessDecisionId = decision.Id,
                request.ExtensionOfLeaseId,
                request.OrganizationId,
                request.CollectionId,
                request.CipherId,
                request.RequesterId,
                request.NotBefore,
                request.NotAfter,
                request.Reason,
                Now = now,
            },
            commandType: CommandType.StoredProcedure);

        return (AccessLeaseExtendOutcome)result;
    }
}
