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

public class LeaseRequestRepository : Repository<LeaseRequest, Guid>, ILeaseRequestRepository
{
    public LeaseRequestRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public LeaseRequestRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<LeaseRequest?> GetActivePendingByRequesterIdCipherIdAsync(Guid requesterId, Guid cipherId)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<LeaseRequest>(
            $"[{Schema}].[LeaseRequest_ReadActivePendingByRequesterIdCipherId]",
            new { RequesterId = requesterId, CipherId = cipherId },
            commandType: CommandType.StoredProcedure);

        return results.FirstOrDefault();
    }

    public async Task<ICollection<InboxLeaseRequestDetails>> GetManyInboxPendingByCollectionIdsAsync(IEnumerable<Guid> collectionIds)
    {
        var ids = collectionIds.ToList();
        if (ids.Count == 0)
        {
            return new List<InboxLeaseRequestDetails>();
        }

        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<InboxLeaseRequestDetails>(
            $"[{Schema}].[LeaseRequest_ReadInboxPendingByCollectionIds]",
            new { CollectionIds = ids.ToGuidIdArrayTVP() },
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }

    public async Task<ICollection<InboxLeaseRequestDetails>> GetManyInboxHistoryByCollectionIdsAsync(IEnumerable<Guid> collectionIds, DateTime since)
    {
        var ids = collectionIds.ToList();
        if (ids.Count == 0)
        {
            return new List<InboxLeaseRequestDetails>();
        }

        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<InboxLeaseRequestDetails>(
            $"[{Schema}].[LeaseRequest_ReadInboxHistoryByCollectionIds]",
            new { CollectionIds = ids.ToGuidIdArrayTVP(), Since = since },
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }

    public async Task ResolveWithDecisionAsync(LeaseRequest request, LeaseDecision decision, LeaseRequestStatus status, Lease? lease, DateTime now)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            $"[{Schema}].[LeaseRequest_ResolveWithDecision]",
            new
            {
                LeaseRequestId = request.Id,
                Status = status,
                LeaseDecisionId = decision.Id,
                ApproverId = decision.ApproverId,
                Decision = decision.Decision,
                decision.Comment,
                LeaseId = lease?.Id,
                Now = now,
            },
            commandType: CommandType.StoredProcedure);
    }
}
