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
        requestEntity.Action = AccessRequestAction.Approved;
        requestEntity.ActionDate = request.CreationDate;

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

    public async Task<CoreEntity?> GetActivePendingByRequesterIdCipherIdAsync(Guid requesterId, Guid cipherId, DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // Open (no action recorded) and still answerable: a lapsed unanswered request is derived Expired, so it no
        // longer blocks a fresh submission or props up a dead pending banner.
        var request = await dbContext.AccessRequests
            .Where(r => r.RequesterId == requesterId
                && r.CipherId == cipherId
                && r.Action == AccessRequestAction.None
                && r.NotAfter > now)
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
                && r.Action == AccessRequestAction.Approved
                && r.NotAfter > now
                && r.ExtensionOfLeaseId == null
                && !dbContext.AccessLeases.Any(l => l.AccessRequestId == r.Id))
            .OrderByDescending(r => r.CreationDate)
            .AsNoTracking()
            .FirstOrDefaultAsync();
        return Mapper.Map<CoreEntity>(request);
    }

    public async Task<AccessRequestDetails?> GetDetailsByIdAsync(Guid id, DateTime now)
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

        var usersById = await GetUsersByIdAsync(dbContext, new[] { request.RequesterId });
        var producedLeases = await GetLatestLeaseByRequestIdsAsync(dbContext, new[] { id });
        var decisionsByRequest = await GetDecisionsByRequestIdsAsync(dbContext, new[] { id });

        return ProjectDetails(request, producedLeases.GetValueOrDefault(id), now, usersById, decisionsByRequest);
    }

    public async Task<ICollection<AccessRequestDetails>> GetManyByRequesterIdAsync(Guid requesterId, DateTime? since,
        DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // Caller-scoped self-read: cipher/collection/requester names are omitted since they come from the caller's
        // own vault. Bounded like AccessRequest_ReadManyByRequesterId: history is retention-windowed, but anything
        // still actionable (open or approved, unlapsed) stays visible regardless of age.
        var requests = await dbContext.AccessRequests
            .Where(r => r.RequesterId == requesterId
                && (since == null
                    || r.CreationDate >= since
                    || ((r.Action == AccessRequestAction.None || r.Action == AccessRequestAction.Approved)
                        && r.NotAfter > now)))
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

        return requests
            .Select(request => ProjectDetails(
                request, producedLeasesByRequest.GetValueOrDefault(request.Id), now,
                decisionsByRequest: decisionsByRequest))
            .ToList();
    }

    public async Task<ICollection<AccessRequestDetails>> GetManyInboxPendingByCollectionIdsAsync(IEnumerable<Guid> collectionIds, DateTime now)
    {
        var ids = collectionIds.ToList();
        if (ids.Count == 0)
        {
            return new List<AccessRequestDetails>();
        }

        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // Actionable only: no action recorded and window still open; a lapsed row is derived Expired and belongs
        // to history instead. An open request carries no approvers yet.
        var requests = await dbContext.AccessRequests
            .Where(r => ids.Contains(r.CollectionId)
                && r.Action == AccessRequestAction.None
                && r.NotAfter > now)
            .AsNoTracking()
            .ToListAsync();

        if (requests.Count == 0)
        {
            return new List<AccessRequestDetails>();
        }

        var usersById = await GetUsersByIdAsync(dbContext, requests.Select(r => r.RequesterId));

        // No lease lookup: an open request has never been activated (a lease is only ever minted from Approved),
        // so there's nothing to find.
        return requests
            .Select(request => ProjectDetails(request, lease: null, now, usersById))
            .ToList();
    }

    public async Task<ICollection<AccessRequestDetails>> GetManyInboxHistoryByCollectionIdsAsync(IEnumerable<Guid> collectionIds, DateTime since, DateTime now)
    {
        var ids = collectionIds.ToList();
        if (ids.Count == 0)
        {
            return new List<AccessRequestDetails>();
        }

        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // The exact complement of the pending read: an action recorded, or a window lapsed with none (derived
        // Expired).
        var requests = await dbContext.AccessRequests
            .Where(r => ids.Contains(r.CollectionId)
                && (r.Action != AccessRequestAction.None || r.NotAfter <= now)
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

        return requests
            .Select(request => ProjectDetails(
                request, producedLeasesByRequest.GetValueOrDefault(request.Id), now,
                usersById, decisionsByRequest))
            .ToList();
    }

    public async Task ResolveWithDecisionAsync(CoreEntity request, AccessDecision decision, AccessRequestAction action, DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        // The WHERE guard keeps the write idempotent under a race, so a second approver can't move an
        // already-resolved request; the decision is recorded only when the transition actually happened.
        var rowsAffected = await dbContext.AccessRequests
            .Where(r => r.Id == request.Id && r.Action == AccessRequestAction.None)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Action, action)
                .SetProperty(r => r.ActionDate, now));

        if (rowsAffected > 0)
        {
            // The verdict belongs to the request being resolved, so its request id is derived from that request
            // rather than trusted from the caller's copy — matching the stored procedure, which reuses its single
            // @AccessRequestId for both the guarded UPDATE and the decision insert.
            var decisionEntity = Mapper.Map<EfDecision>(decision);
            decisionEntity.AccessRequestId = request.Id;
            decisionEntity.DeciderKind = AccessDeciderKind.Human;
            decisionEntity.ConditionKind = null;
            decisionEntity.EvaluationContext = null;
            decisionEntity.CreationDate = now;

            await dbContext.AccessDecisions.AddAsync(decisionEntity);
            await dbContext.SaveChangesAsync();
        }

        await transaction.CommitAsync();
    }

    public async Task CancelAsync(Guid id, DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // Requires a transaction so the claim's row lock holds across both statements, not just its own.
        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        await ClaimRequestRowAsync(dbContext, id);

        // No AccessDecision is written -- a cancellation is the requester acting on their own request, not an
        // approver verdict.
        await RetractableRequests(dbContext, id, now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Action, AccessRequestAction.Cancelled)
                .SetProperty(r => r.ActionDate, now));

        await transaction.CommitAsync();
    }

    public async Task CancelWithDecisionAsync(CoreEntity request, AccessDecision decision, DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        await ClaimRequestRowAsync(dbContext, request.Id);

        // Inserted only alongside an actual transition, so a no-op never orphans a decision.
        var rowsAffected = await RetractableRequests(dbContext, request.Id, now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Action, AccessRequestAction.Denied)
                .SetProperty(r => r.ActionDate, now));

        if (rowsAffected > 0)
        {
            // As in ResolveWithDecisionAsync, the retraction's verdict is bound to the request being retracted
            // rather than to whatever the caller's decision names.
            var decisionEntity = Mapper.Map<EfDecision>(decision);
            decisionEntity.AccessRequestId = request.Id;
            decisionEntity.DeciderKind = AccessDeciderKind.Human;
            decisionEntity.ConditionKind = null;
            decisionEntity.EvaluationContext = null;
            decisionEntity.CreationDate = now;

            await dbContext.AccessDecisions.AddAsync(decisionEntity);
            await dbContext.SaveChangesAsync();
        }

        await transaction.CommitAsync();
    }

    /// <summary>
    /// Takes the request row's write lock so a retraction's lease probe can't run before the row is held; both
    /// retraction writes test lease existence via a subquery the provider could otherwise evaluate before touching
    /// AccessRequest at all, letting a concurrent
    /// <see cref="Bit.Infrastructure.EntityFramework.Pam.Repositories.AccessLeaseRepository.CreateFromApprovedRequestAsync"/>
    /// mint and commit in the gap.
    /// </summary>
    /// <remarks>
    /// By the time the guarded update runs, activation either is still blocked behind this transaction (and then
    /// fails its own CAS), or already committed and the probe sees the lease it minted. EF has no portable UPDLOCK
    /// hint, so the lock is taken by writing the row (a no-op write); this must run inside a transaction.
    ///
    /// Must stay a separate statement, not folded into the guard: on PostgreSQL, a single statement that blocks on a
    /// concurrently-updated row resumes under ReadCommitted with its subqueries still on the original snapshot, so
    /// the guard would miss the lease that appeared alongside the row it re-reads.
    /// </remarks>
    private static Task ClaimRequestRowAsync(DatabaseContext dbContext, Guid id)
        => dbContext.AccessRequests
            .Where(r => r.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Action, r => r.Action));

    /// <summary>
    /// The requests a retraction may still settle: open or approved-unactivated, window not lapsed, no produced
    /// lease. Excludes a request with a produced lease (revoke that instead) or a lapsed window (already
    /// derived-Expired). Shared by both cancel writes so the guarded set can't drift between them.
    /// </summary>
    private static IQueryable<EfModel> RetractableRequests(DatabaseContext dbContext, Guid id, DateTime now)
        => dbContext.AccessRequests
            .Where(r => r.Id == id
                && (r.Action == AccessRequestAction.None || r.Action == AccessRequestAction.Approved)
                && r.NotAfter > now
                && !dbContext.AccessLeases.Any(l => l.AccessRequestId == id));

    public async Task<int> CountExtensionsByLeaseIdAsync(Guid leaseId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        return await dbContext.AccessRequests.CountAsync(r => r.ExtensionOfLeaseId == leaseId);
    }

    public async Task<AccessLeaseExtendOutcome> CreateApprovedExtensionAsync(CoreEntity request, AccessDecision decision, DateTime now, string? denialComment)
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
                && l.Action == AccessLeaseAction.None
                && l.NotAfter > now)
            .FirstOrDefaultAsync();

        if (lease is null)
        {
            // Nothing left to extend, but still recorded as an answerable denied request; the requested window is
            // stored though never applied to any lease.
            await WriteExtensionAsync(dbContext, request, decision, now,
                AccessRequestAction.Denied, AccessDecisionVerdict.Deny, denialComment);

            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

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
        await WriteExtensionAsync(dbContext, request, decision, now,
            AccessRequestAction.Approved, AccessDecisionVerdict.Approve, comment: null);

        lease.NotAfter = request.NotAfter;

        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        return AccessLeaseExtendOutcome.Extended;
    }

    /// <summary>
    /// Stages the extension request and its automatic decision for insert. The caller supplies one set of entities
    /// for both outcomes; the recorded action (approved, or denied since the parent lease is gone) is decided here,
    /// not trusted from the caller.
    /// </summary>
    private async Task WriteExtensionAsync(DatabaseContext dbContext, CoreEntity request, AccessDecision decision,
        DateTime now, AccessRequestAction action, AccessDecisionVerdict verdict, string? comment)
    {
        var requestEntity = Mapper.Map<EfModel>(request);
        requestEntity.Action = action;
        requestEntity.CreationDate = now;
        requestEntity.ActionDate = now;

        // The automatic decision belongs to the extension request being created, so its request id is derived from
        // that request rather than trusted from the caller's copy — matching the stored procedure, which reuses its
        // @AccessRequestId for both inserts.
        var decisionEntity = Mapper.Map<EfDecision>(decision);
        decisionEntity.AccessRequestId = requestEntity.Id;
        decisionEntity.DeciderKind = AccessDeciderKind.Automatic;
        decisionEntity.ApproverId = null;
        decisionEntity.ConditionKind = null;
        decisionEntity.Verdict = verdict;
        decisionEntity.Comment = comment;
        decisionEntity.EvaluationContext = null;
        decisionEntity.CreationDate = now;

        await dbContext.AccessRequests.AddAsync(requestEntity);
        await dbContext.AccessDecisions.AddAsync(decisionEntity);
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
    /// The one path from a raw EF row to the read model: maps scalars, stamps derived statuses, and attaches
    /// identity/decisions. Every read funnels through here so a projection can't silently skip stamping.
    /// Mirrors the Dapper side's DetailsRow.Derive.
    /// </summary>
    private AccessRequestDetails ProjectDetails(EfModel request, EfLease? lease, DateTime now,
        Dictionary<Guid, (string? Name, string? Email)>? usersById = null,
        Dictionary<Guid, List<AccessRequestDecision>>? decisionsByRequest = null)
    {
        var details = Mapper.Map<AccessRequestDetails>(request);
        if (usersById is not null)
        {
            ApplyRequesterIdentity(details, request, usersById);
        }
        ApplyDerivedStatuses(details, request, lease, now);
        if (decisionsByRequest is not null && decisionsByRequest.TryGetValue(request.Id, out var decisions))
        {
            details.Decisions = decisions;
        }
        return details;
    }

    /// <summary>
    /// Stamps the derived statuses onto a details projection at the repository boundary. The derivation itself (and
    /// why the lease's <em>own</em> action/NotAfter feed it) lives in
    /// <see cref="AccessRequestDetails.StampDerivedStatuses"/>, shared with the Dapper repository's row hydration.
    /// </summary>
    private static void ApplyDerivedStatuses(AccessRequestDetails details, EfModel request, EfLease? lease, DateTime now)
        => details.StampDerivedStatuses(
            request.Action, lease is null ? null : (lease.Id, lease.Action, lease.NotAfter), now);

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
                    ApproverId = d.ApproverId,
                    Name = identity.Name,
                    Email = identity.Email,
                    Comment = d.Comment,
                    Verdict = d.Verdict,
                    DecidedAt = d.CreationDate,
                };
            }).ToList());
    }
}
