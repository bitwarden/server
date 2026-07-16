using AutoMapper;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CoreEntity = Bit.Pam.Entities.AccessRequest;
using EfDecision = Bit.Infrastructure.EntityFramework.Pam.Models.AccessDecision;
using EfLease = Bit.Infrastructure.EntityFramework.Pam.Models.AccessLease;
using EfModel = Bit.Infrastructure.EntityFramework.Pam.Models.AccessRequest;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Repositories;

public class AccessRequestRepository : Repository<CoreEntity, EfModel, Guid>, IAccessRequestRepository
{
    public AccessRequestRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, context => context.AccessRequests)
    { }

    public async Task CreateAutoApprovedAsync(CoreEntity request, AccessDecision decision)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        // The request is created already resolved (Approved). ExtensionOfLeaseId stays NULL: it is reserved for
        // extension requests; provenance for an original lease flows the other way, via AccessLease.AccessRequestId.
        var requestEntity = Mapper.Map<EfModel>(request);
        requestEntity.ExtensionOfLeaseId = null;
        requestEntity.Status = AccessRequestStatus.Approved;
        requestEntity.ResolvedDate = request.CreationDate;

        var decisionEntity = Mapper.Map<EfDecision>(decision);
        decisionEntity.AccessRequestId = request.Id;
        decisionEntity.DeciderKind = AccessDeciderKind.Automatic;
        decisionEntity.ApproverId = null;
        decisionEntity.Verdict = AccessDecisionVerdict.Approve;
        decisionEntity.Comment = null;
        decisionEntity.EvaluationContext = null;
        decisionEntity.CreationDate = request.CreationDate;

        await dbContext.AccessRequests.AddAsync(requestEntity);
        await dbContext.AccessDecisions.AddAsync(decisionEntity);
        await dbContext.SaveChangesAsync();

        await transaction.CommitAsync();
    }

    public async Task<CoreEntity?> GetActivePendingByRequesterIdCipherIdAsync(Guid requesterId, Guid cipherId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var request = await dbContext.AccessRequests
            .Where(r => r.RequesterId == requesterId
                && r.CipherId == cipherId
                && r.Status == AccessRequestStatus.Pending)
            .OrderByDescending(r => r.CreationDate)
            .AsNoTracking()
            .FirstOrDefaultAsync();
        return Mapper.Map<CoreEntity>(request);
    }

    public async Task<CoreEntity?> GetActiveApprovedByRequesterIdCipherIdAsync(Guid requesterId, Guid cipherId, DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // Future windows are included (the client shows the upcoming window); lapsed windows are excluded so the
        // client never offers an activation the server would reject. Extension requests are excluded: an approved
        // extension pushes its parent lease's end out in place and never produces a lease of its own.
        var request = await dbContext.AccessRequests
            .Where(r => r.RequesterId == requesterId
                && r.CipherId == cipherId
                && r.Status == AccessRequestStatus.Approved
                && r.NotAfter > now
                && r.ExtensionOfLeaseId == null
                && !dbContext.AccessLeases.Any(l => l.AccessRequestId == r.Id))
            .OrderByDescending(r => r.CreationDate)
            .AsNoTracking()
            .FirstOrDefaultAsync();
        return Mapper.Map<CoreEntity>(request);
    }

    public async Task<AccessRequestDetails?> GetDetailsByIdAsync(Guid id)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var request = await dbContext.AccessRequests
            .Where(r => r.Id == id)
            .AsNoTracking()
            .FirstOrDefaultAsync();
        if (request is null)
        {
            return null;
        }

        var details = Mapper.Map<AccessRequestDetails>(request);
        var usersById = await GetUsersByIdAsync(dbContext, new[] { request.RequesterId });
        ApplyRequesterIdentity(details, request, usersById);

        var producedLeases = await GetLatestLeaseByRequestIdsAsync(dbContext, new[] { id });
        if (producedLeases.TryGetValue(id, out var lease))
        {
            details.ProducedLeaseId = lease.Id;
            details.ProducedLeaseStatus = lease.Status;
        }

        var decisionsByRequest = await GetDecisionsByRequestIdsAsync(dbContext, new[] { id });
        if (decisionsByRequest.TryGetValue(id, out var decisions))
        {
            details.Decisions = decisions;
        }

        return details;
    }

    public async Task<ICollection<AccessRequestDetails>> GetManyByRequesterIdAsync(Guid requesterId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // Caller-scoped self-read: the cipher/collection/requester display-name joins are intentionally omitted
        // (those names come from the caller's local vault, and the requester is the caller).
        var requests = await dbContext.AccessRequests
            .Where(r => r.RequesterId == requesterId)
            .OrderByDescending(r => r.CreationDate)
            .Take(250)
            .AsNoTracking()
            .ToListAsync();

        if (requests.Count == 0)
        {
            return new List<AccessRequestDetails>();
        }

        var requestIds = requests.Select(r => r.Id).ToList();
        var producedLeasesByRequest = await GetLatestLeaseByRequestIdsAsync(dbContext, requestIds);
        var decisionsByRequest = await GetDecisionsByRequestIdsAsync(dbContext, requestIds);

        return requests.Select(request =>
        {
            var details = Mapper.Map<AccessRequestDetails>(request);
            if (producedLeasesByRequest.TryGetValue(request.Id, out var lease))
            {
                details.ProducedLeaseId = lease.Id;
                details.ProducedLeaseStatus = lease.Status;
            }
            if (decisionsByRequest.TryGetValue(request.Id, out var decisions))
            {
                details.Decisions = decisions;
            }
            return details;
        }).ToList();
    }

    public async Task<ICollection<AccessRequestDetails>> GetManyInboxPendingByCollectionIdsAsync(IEnumerable<Guid> collectionIds)
    {
        var ids = collectionIds.ToList();
        if (ids.Count == 0)
        {
            return new List<AccessRequestDetails>();
        }

        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // A pending request has not been decided by anyone yet, so it carries no approvers (the decisions list stays
        // at its default empty value); only the resolved reads populate a decision list.
        var requests = await dbContext.AccessRequests
            .Where(r => ids.Contains(r.CollectionId) && r.Status == AccessRequestStatus.Pending)
            .AsNoTracking()
            .ToListAsync();

        if (requests.Count == 0)
        {
            return new List<AccessRequestDetails>();
        }

        var requestIds = requests.Select(r => r.Id).ToList();
        var usersById = await GetUsersByIdAsync(dbContext, requests.Select(r => r.RequesterId));
        var producedLeasesByRequest = await GetLatestLeaseByRequestIdsAsync(dbContext, requestIds);

        return requests.Select(request =>
        {
            var details = Mapper.Map<AccessRequestDetails>(request);
            ApplyRequesterIdentity(details, request, usersById);
            if (producedLeasesByRequest.TryGetValue(request.Id, out var lease))
            {
                details.ProducedLeaseId = lease.Id;
                details.ProducedLeaseStatus = lease.Status;
            }
            return details;
        }).ToList();
    }

    public async Task<ICollection<AccessRequestDetails>> GetManyInboxHistoryByCollectionIdsAsync(IEnumerable<Guid> collectionIds, DateTime since)
    {
        var ids = collectionIds.ToList();
        if (ids.Count == 0)
        {
            return new List<AccessRequestDetails>();
        }

        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var requests = await dbContext.AccessRequests
            .Where(r => ids.Contains(r.CollectionId)
                && r.Status != AccessRequestStatus.Pending
                && r.CreationDate >= since)
            .AsNoTracking()
            .ToListAsync();

        if (requests.Count == 0)
        {
            return new List<AccessRequestDetails>();
        }

        var requestIds = requests.Select(r => r.Id).ToList();
        var usersById = await GetUsersByIdAsync(dbContext, requests.Select(r => r.RequesterId));
        var producedLeasesByRequest = await GetLatestLeaseByRequestIdsAsync(dbContext, requestIds);
        var decisionsByRequest = await GetDecisionsByRequestIdsAsync(dbContext, requestIds);

        return requests.Select(request =>
        {
            var details = Mapper.Map<AccessRequestDetails>(request);
            ApplyRequesterIdentity(details, request, usersById);
            if (producedLeasesByRequest.TryGetValue(request.Id, out var lease))
            {
                details.ProducedLeaseId = lease.Id;
                details.ProducedLeaseStatus = lease.Status;
            }
            if (decisionsByRequest.TryGetValue(request.Id, out var decisions))
            {
                details.Decisions = decisions;
            }
            return details;
        }).ToList();
    }

    public async Task ResolveWithDecisionAsync(CoreEntity request, AccessDecision decision, AccessRequestStatus status, DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        // The caller has already verified (and the application enforces) that the request is still Pending; the
        // WHERE guard keeps the write idempotent under a race so a second approver can't move an already-resolved
        // request. The decision is inserted unconditionally, matching the stored procedure (no @@ROWCOUNT guard).
        await dbContext.AccessRequests
            .Where(r => r.Id == request.Id && r.Status == AccessRequestStatus.Pending)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, status)
                .SetProperty(r => r.ResolvedDate, now));

        var decisionEntity = Mapper.Map<EfDecision>(decision);
        decisionEntity.DeciderKind = AccessDeciderKind.Human;
        decisionEntity.ConditionKind = null;
        decisionEntity.EvaluationContext = null;
        decisionEntity.CreationDate = now;

        await dbContext.AccessDecisions.AddAsync(decisionEntity);
        await dbContext.SaveChangesAsync();

        await transaction.CommitAsync();
    }

    public async Task CancelAsync(Guid id, DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // Refuses a request that has already produced a lease (that access is governed by the lease, which must be
        // revoked instead). No AccessDecision is written -- a cancellation is the requester acting on their own
        // request, not an approver verdict.
        await dbContext.AccessRequests
            .Where(r => r.Id == id
                && (r.Status == AccessRequestStatus.Pending || r.Status == AccessRequestStatus.Approved)
                && !dbContext.AccessLeases.Any(l => l.AccessRequestId == id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, AccessRequestStatus.Cancelled)
                .SetProperty(r => r.ResolvedDate, now));
    }

    public async Task CancelWithDecisionAsync(CoreEntity request, AccessDecision decision, DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        // Refuses a request that has produced a lease (governed by the lease -- revoke instead). The decision is
        // inserted only when the transition actually happened, so a no-op never orphans an AccessDecision.
        var rowsAffected = await dbContext.AccessRequests
            .Where(r => r.Id == request.Id
                && (r.Status == AccessRequestStatus.Pending || r.Status == AccessRequestStatus.Approved)
                && !dbContext.AccessLeases.Any(l => l.AccessRequestId == request.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, AccessRequestStatus.Denied)
                .SetProperty(r => r.ResolvedDate, now));

        if (rowsAffected > 0)
        {
            var decisionEntity = Mapper.Map<EfDecision>(decision);
            decisionEntity.DeciderKind = AccessDeciderKind.Human;
            decisionEntity.ConditionKind = null;
            decisionEntity.EvaluationContext = null;
            decisionEntity.CreationDate = now;

            await dbContext.AccessDecisions.AddAsync(decisionEntity);
            await dbContext.SaveChangesAsync();
        }

        await transaction.CommitAsync();
    }

    public async Task<int> CountExtensionsByLeaseIdAsync(Guid leaseId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        return await dbContext.AccessRequests.CountAsync(r => r.ExtensionOfLeaseId == leaseId);
    }

    public async Task<AccessLeaseExtendOutcome> CreateApprovedExtensionAsync(CoreEntity request, AccessDecision decision, DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // A Serializable transaction is the closest cross-provider approximation of the stored procedure's per-lease
        // UPDLOCK/HOLDLOCK range lock: it keeps a concurrent extension of the same lease from reading a
        // pre-extension state and double-committing. Unlike the SQL Server proc (which blocks a concurrent caller
        // until this transaction commits, then re-evaluates deterministically), a losing concurrent transaction here
        // may instead fail at commit time with a provider-level serialization error (e.g. Postgres 40001) rather
        // than cleanly returning AlreadyExtended -- callers should be prepared to treat such an exception as a
        // conflict.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

        var extensionOfLeaseId = request.ExtensionOfLeaseId;
        var lease = await dbContext.AccessLeases
            .Where(l => l.Id == extensionOfLeaseId
                && l.RequesterId == request.RequesterId
                && l.Status == AccessLeaseStatus.Active
                && l.NotAfter > now)
            .FirstOrDefaultAsync();

        if (lease is null)
        {
            await transaction.RollbackAsync();
            return AccessLeaseExtendOutcome.LeaseNotActive;
        }

        // A lease may be extended exactly once.
        var alreadyExtended = await dbContext.AccessRequests
            .AnyAsync(r => r.ExtensionOfLeaseId == extensionOfLeaseId);
        if (alreadyExtended)
        {
            await transaction.RollbackAsync();
            return AccessLeaseExtendOutcome.AlreadyExtended;
        }

        // The request's window spans the extension ([old lease end] .. [new lease end]); its NotAfter is the
        // lease's new end. No new lease is minted -- extending reuses the existing lease, preserving the
        // single-active-lease invariant.
        var requestEntity = Mapper.Map<EfModel>(request);
        requestEntity.Status = AccessRequestStatus.Approved;
        requestEntity.CreationDate = now;
        requestEntity.ResolvedDate = now;

        var decisionEntity = Mapper.Map<EfDecision>(decision);
        decisionEntity.DeciderKind = AccessDeciderKind.Automatic;
        decisionEntity.ApproverId = null;
        decisionEntity.ConditionKind = null;
        decisionEntity.Verdict = AccessDecisionVerdict.Approve;
        decisionEntity.Comment = null;
        decisionEntity.EvaluationContext = null;
        decisionEntity.CreationDate = now;

        await dbContext.AccessRequests.AddAsync(requestEntity);
        await dbContext.AccessDecisions.AddAsync(decisionEntity);

        lease.NotAfter = request.NotAfter;

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        return AccessLeaseExtendOutcome.Extended;
    }

    /// <summary>
    /// Batch-loads the display name/email for a set of user ids (used to denormalize requester/approver identity),
    /// keyed by user id.
    /// </summary>
    private static async Task<Dictionary<Guid, (string? Name, string? Email)>> GetUsersByIdAsync(
        DatabaseContext dbContext, IEnumerable<Guid> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, (string? Name, string? Email)>();
        }

        return (await dbContext.Users
                .Where(u => ids.Contains(u.Id))
                .Select(u => new { u.Id, u.Name, u.Email })
                .AsNoTracking()
                .ToListAsync())
            .ToDictionary(u => u.Id, u => (u.Name, (string?)u.Email));
    }

    private static void ApplyRequesterIdentity(AccessRequestDetails details, EfModel request,
        Dictionary<Guid, (string? Name, string? Email)> usersById)
    {
        if (usersById.TryGetValue(request.RequesterId, out var identity))
        {
            details.RequesterName = identity.Name;
            details.RequesterEmail = identity.Email;
        }
    }

    /// <summary>
    /// Batch-loads the most recently created lease per request id (a request produces at most one lease, ever;
    /// picking the latest mirrors the stored procedures' <c>OUTER APPLY ... ORDER BY CreationDate DESC</c> belt-and-braces
    /// guard), keyed by AccessRequestId.
    /// </summary>
    private static async Task<Dictionary<Guid, EfLease>> GetLatestLeaseByRequestIdsAsync(
        DatabaseContext dbContext, IEnumerable<Guid> requestIds)
    {
        var ids = requestIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, EfLease>();
        }

        var leases = await dbContext.AccessLeases
            .Where(l => ids.Contains(l.AccessRequestId))
            .AsNoTracking()
            .ToListAsync();

        return leases
            .GroupBy(l => l.AccessRequestId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(l => l.CreationDate).First());
    }

    /// <summary>
    /// Batch-loads every decision (human or automatic) for a set of request ids, ordered oldest-first within each
    /// request, with a human decision's identity denormalized from the User join -- mirroring the stored
    /// procedures' second decision result set.
    /// </summary>
    private static async Task<Dictionary<Guid, List<AccessRequestDecision>>> GetDecisionsByRequestIdsAsync(
        DatabaseContext dbContext, IEnumerable<Guid> requestIds)
    {
        var ids = requestIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, List<AccessRequestDecision>>();
        }

        var decisions = await dbContext.AccessDecisions
            .Where(d => ids.Contains(d.AccessRequestId))
            .OrderBy(d => d.AccessRequestId)
            .ThenBy(d => d.CreationDate)
            .AsNoTracking()
            .ToListAsync();

        var approverIds = decisions
            .Where(d => d.ApproverId.HasValue)
            .Select(d => d.ApproverId!.Value);
        var usersById = await GetUsersByIdAsync(dbContext, approverIds);

        return decisions
            .GroupBy(d => d.AccessRequestId)
            .ToDictionary(g => g.Key, g => g.Select(d =>
            {
                (string? Name, string? Email) identity = default;
                if (d.ApproverId.HasValue)
                {
                    usersById.TryGetValue(d.ApproverId.Value, out identity);
                }

                return new AccessRequestDecision
                {
                    DeciderKind = d.DeciderKind,
                    Id = d.ApproverId,
                    Name = identity.Name,
                    Email = identity.Email,
                    Comment = d.Comment,
                    Verdict = d.Verdict,
                    DecidedAt = d.CreationDate,
                };
            }).ToList());
    }
}
