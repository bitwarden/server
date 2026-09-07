using AutoMapper;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CoreEntity = Bit.Pam.Entities.AccessLease;
using EfDecision = Bit.Infrastructure.EntityFramework.Pam.Models.AccessDecision;
using EfLeaseExpirySweep = Bit.Infrastructure.EntityFramework.Pam.Models.PamLeaseExpirySweep;
using EfModel = Bit.Infrastructure.EntityFramework.Pam.Models.AccessLease;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Repositories;

public class AccessLeaseRepository : Repository<CoreEntity, EfModel, Guid>, IAccessLeaseRepository
{
    // Bounds CreateFromApprovedRequestAsync's retry of provider serialization failures.
    private const int MaxMintAttempts = 3;

    public AccessLeaseRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, context => context.AccessLeases)
    { }

    /// <summary>
    /// The live-lease predicate as a shared, EF-translatable expression: no early end and the window open at
    /// <paramref name="now"/>. Every EF read for current authorization composes this; the stored procedures carry
    /// the same predicate and must not drift.
    /// </summary>
    private static System.Linq.Expressions.Expression<Func<EfModel, bool>> LiveAt(DateTime now)
        => l => l.Action == AccessLeaseAction.None && l.NotBefore <= now && l.NotAfter > now;

    public async Task<CoreEntity?> GetByAccessRequestIdAsync(Guid accessRequestId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // A request produces at most one lease ([IX_AccessLease_AccessRequestId] is unique); ordering by
        // CreationDate DESC + first is belt and braces, mirroring the stored procedure's TOP 1.
        var lease = await dbContext.AccessLeases
            .Where(l => l.AccessRequestId == accessRequestId)
            .OrderByDescending(l => l.CreationDate)
            .AsNoTracking()
            .FirstOrDefaultAsync();
        return Mapper.Map<CoreEntity>(lease);
    }

    public async Task<CoreEntity?> GetActiveByRequesterIdCipherIdAsync(Guid requesterId, Guid cipherId, DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var lease = await dbContext.AccessLeases
            .Where(l => l.RequesterId == requesterId && l.CipherId == cipherId)
            .Where(LiveAt(now))
            .OrderByDescending(l => l.NotAfter)
            .AsNoTracking()
            .FirstOrDefaultAsync();
        return Mapper.Map<CoreEntity>(lease);
    }

    public async Task<ICollection<CoreEntity>> GetManyActiveByRequesterIdAsync(Guid requesterId, DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var leases = await dbContext.AccessLeases
            .Where(l => l.RequesterId == requesterId)
            .Where(LiveAt(now))
            .OrderBy(l => l.NotAfter)
            .AsNoTracking()
            .ToListAsync();
        return Mapper.Map<List<CoreEntity>>(leases);
    }

    public async Task<CoreEntity?> GetActiveByCipherIdAsync(Guid cipherId, DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // Latest-ending, across all members, since the singleton guard blocks until the last in-window lease frees the
        // slot. Cipher-scoped, like that guard.
        var lease = await dbContext.AccessLeases
            .Where(l => l.CipherId == cipherId)
            .Where(LiveAt(now))
            .OrderByDescending(l => l.NotAfter)
            .AsNoTracking()
            .FirstOrDefaultAsync();
        return Mapper.Map<CoreEntity>(lease);
    }

    public async Task<ICollection<CoreEntity>> GetManyActiveByCollectionIdsAsync(IEnumerable<Guid> collectionIds, DateTime now)
    {
        var ids = collectionIds.ToList();
        if (ids.Count == 0)
        {
            return new List<CoreEntity>();
        }

        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // Governance view: every currently-active lease on the supplied (caller-manageable) collections, across all
        // members -- not just the caller's own.
        var leases = await dbContext.AccessLeases
            .Where(l => ids.Contains(l.CollectionId))
            .Where(LiveAt(now))
            .OrderBy(l => l.NotAfter)
            .AsNoTracking()
            .ToListAsync();
        return Mapper.Map<List<CoreEntity>>(leases);
    }

    public async Task<ICollection<CoreEntity>> GetManyEndedByCollectionIdsAsync(IEnumerable<Guid> collectionIds,
        DateTime since, DateTime now)
    {
        var ids = collectionIds.ToList();
        if (ids.Count == 0)
        {
            return new List<CoreEntity>();
        }

        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // End is RevokedDate for an early end, NotAfter for a natural close; mirrors the matching stored procedure.
        var leases = await dbContext.AccessLeases
            .Where(l => ids.Contains(l.CollectionId)
                && (
                    // Ended early (Revoked, Cancelled): its end is RevokedDate, whatever its window says.
                    ((l.Action == AccessLeaseAction.Revoked || l.Action == AccessLeaseAction.Cancelled) && l.RevokedDate >= since)
                    // Window closed on its own (end = NotAfter); byte 1 (retired stored Expired) is deliberately not
                    // matched since ComputeLeaseStatus has no arm for it and would throw.
                    || (l.Action == AccessLeaseAction.None && l.NotAfter <= now && l.NotAfter >= since)
                ))
            .OrderByDescending(l => l.RevokedDate ?? l.NotAfter)
            .AsNoTracking()
            .ToListAsync();

        return Mapper.Map<List<CoreEntity>>(leases);
    }

    /// <remarks>
    /// Retried on a fresh transaction after a provider serialization failure; bounded retries propagate on exhaustion.
    /// Applies only under <paramref name="enforceSingleActiveLease"/>; see <see cref="MintFromApprovedRequestAsync"/>.
    /// </remarks>
    public async Task<AccessLeaseMintOutcome> CreateFromApprovedRequestAsync(CoreEntity lease, DateTime now,
        bool enforceSingleActiveLease)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await MintFromApprovedRequestAsync(lease, now, enforceSingleActiveLease);
            }
            catch (Exception e) when (attempt < MaxMintAttempts && IsSerializationFailure(e))
            {
                // The aborted transaction committed nothing, so the next attempt starts from a fresh scope.
            }
        }
    }

    private async Task<AccessLeaseMintOutcome> MintFromApprovedRequestAsync(CoreEntity lease, DateTime now,
        bool enforceSingleActiveLease)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // Serializable covers only the per-cipher guard, matching the procedure's range lock. Other writes are
        // protected by the claim below; wider Serializable use aborted unrelated writers, so this restores parity
        // with [AccessLease_CreateFromApprovedRequest].
        var isolation = enforceSingleActiveLease
            ? System.Data.IsolationLevel.Serializable
            : System.Data.IsolationLevel.ReadCommitted;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(isolation);

        try
        {
            // Claims the request row first, closing a write-skew race with retraction (mirrors the procedure's
            // claim); must stay ahead of the singleton guard, since reversing that lock order would deadlock.
            // Serializable alone doesn't catch this, as SSI only detects cycles among Serializable transactions.
            // Preconditions re-check in one CAS, so a concurrent retraction yields a clean zero-row outcome.
            var claimed = await dbContext.AccessRequests
                .Where(r => r.Id == lease.AccessRequestId
                    && r.RequesterId == lease.RequesterId
                    && r.Action == AccessRequestAction.Approved
                    // An extension applies in place at approval and never mints its own lease; it stays Approved with no produced lease.
                    && r.ExtensionOfLeaseId == null
                    && r.NotBefore <= now
                    && r.NotAfter > now
                    && !dbContext.AccessLeases.Any(l => l.AccessRequestId == r.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Action, AccessRequestAction.Approved));

            if (claimed == 0)
            {
                await transaction.RollbackAsync();
                return AccessLeaseMintOutcome.PreconditionFailed;
            }

            // The claim already holds the row for this transaction, so this read can't miss or go stale; it supplies
            // the columns the lease is minted from.
            var request = await dbContext.AccessRequests
                .AsNoTracking()
                .FirstAsync(r => r.Id == lease.AccessRequestId);

            if (enforceSingleActiveLease)
            {
                // Cipher comes from the request, not the caller's copy, to prevent a second concurrent lease.
                var conflict = await dbContext.AccessLeases
                    .Where(l => l.CipherId == request.CipherId)
                    .Where(LiveAt(now))
                    .AnyAsync();
                if (conflict)
                {
                    await transaction.RollbackAsync();
                    return AccessLeaseMintOutcome.SingleActiveLeaseConflict;
                }
            }

            var leaseEntity = Mapper.Map<EfModel>(lease);
            leaseEntity.OrganizationId = request.OrganizationId;
            leaseEntity.CollectionId = request.CollectionId;
            leaseEntity.CipherId = request.CipherId;
            leaseEntity.RequesterId = request.RequesterId;
            leaseEntity.Action = AccessLeaseAction.None;
            // Starts at this activation, never backdated to the request's window start (mirrors the procedure).
            // End stays the request's, so late activation shortens the lease.
            leaseEntity.NotBefore = now;
            leaseEntity.NotAfter = request.NotAfter;
            leaseEntity.RevokedDate = null;
            leaseEntity.RevokedBy = null;
            leaseEntity.CreationDate = now;

            await dbContext.AccessLeases.AddAsync(leaseEntity);
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return AccessLeaseMintOutcome.Minted;
        }
        catch (DbUpdateException e) when (IsDuplicateKeyException(e))
        {
            // The unique-index backstop ([IX_AccessLease_AccessRequestId]): a concurrent activation won the race
            // after our application-level precondition check passed. Same outcome as the guard catching it -- the
            // caller re-reads the winner. Anything else propagates: on a path that grants access to Vault Data, a
            // genuine persistence failure must not be reported as a benign outcome.
            await transaction.RollbackAsync();
            return AccessLeaseMintOutcome.PreconditionFailed;
        }
    }

    public async Task RevokeAsync(CoreEntity lease, AccessLeaseAction endAction, AccessDecision auditDecision, DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        // The reason has no dedicated column, so it is preserved as a human AccessDecision (Deny) against the
        // lease's originating request, keeping the audit trail without a schema change. The request id is read from
        // the lease row rather than trusted from the caller's copy, matching the stored procedure's OUTPUT clause.
        var accessRequestId = await dbContext.AccessLeases
            .Where(l => l.Id == lease.Id)
            .Select(l => l.AccessRequestId)
            .FirstOrDefaultAsync();

        // The decision is recorded only when the transition actually happened, so a repeat or losing revoke never
        // appends a Deny verdict for a lease it did not end.
        var rowsAffected = await dbContext.AccessLeases
            .Where(l => l.Id == lease.Id && l.Action == AccessLeaseAction.None)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.Action, endAction)
                .SetProperty(l => l.RevokedDate, now)
                .SetProperty(l => l.RevokedBy, auditDecision.ApproverId));

        if (rowsAffected > 0)
        {
            var decisionEntity = Mapper.Map<EfDecision>(auditDecision);
            decisionEntity.AccessRequestId = accessRequestId;
            decisionEntity.DeciderKind = AccessDeciderKind.Human;
            decisionEntity.ConditionKind = null;
            decisionEntity.Verdict = AccessDecisionVerdict.Deny;
            decisionEntity.EvaluationContext = null;
            decisionEntity.CreationDate = now;

            await dbContext.AccessDecisions.AddAsync(decisionEntity);
            await dbContext.SaveChangesAsync();
        }

        await transaction.CommitAsync();
    }

    public async Task<IReadOnlyList<PamExpiredLease>> ExpireDueAsync(DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // Expiry is derived, not stored; PamLeaseExpirySweep's journal decides which run owns a lease.
        // No stronger isolation needed; a losing sweep's SaveChanges fails on the journal's primary key.
        var due = await dbContext.AccessLeases
            .Where(l => l.Action == AccessLeaseAction.None && l.NotAfter <= now &&
                !dbContext.PamLeaseExpirySweeps.Any(s => s.AccessLeaseId == l.Id))
            .Select(l => new PamExpiredLease
            {
                Id = l.Id,
                OrganizationId = l.OrganizationId,
                CollectionId = l.CollectionId,
                CipherId = l.CipherId,
                RequesterId = l.RequesterId,
                NotBefore = l.NotBefore,
                NotAfter = l.NotAfter,
            })
            .ToListAsync();

        if (due.Count > 0)
        {
            dbContext.PamLeaseExpirySweeps.AddRange(due.Select(l =>
                new EfLeaseExpirySweep { AccessLeaseId = l.Id, SweptDate = now }));
            await dbContext.SaveChangesAsync();
        }

        return due;
    }

    /// <summary>
    /// True when the provider refused the transaction because it could not serialize it against a concurrent one:
    /// PostgreSQL's SSI aborting it at commit, or a deadlock victim elsewhere. The transaction is already gone in
    /// every case, so the only recovery is to run the whole attempt again.
    /// </summary>
    private static bool IsSerializationFailure(Exception e) => e switch
    {
        Npgsql.PostgresException pg => pg.SqlState is "40001" or "40P01",
        MySqlConnector.MySqlException my => my.ErrorCode is MySqlConnector.MySqlErrorCode.LockDeadlock
            or MySqlConnector.MySqlErrorCode.LockWaitTimeout,
        Microsoft.Data.SqlClient.SqlException ms => ms.Errors
            .Cast<Microsoft.Data.SqlClient.SqlError>()
            .Any(error => error.Number is 1205),
        Microsoft.Data.Sqlite.SqliteException lite => lite.SqliteErrorCode is 5 or 6,
        _ => e.InnerException is not null && IsSerializationFailure(e.InnerException),
    };

    /// <summary>
    /// True when the write failed because a duplicate key was inserted -- here, the unique
    /// [IX_AccessLease_AccessRequestId] backstop tripping because a concurrent activation minted this request's
    /// lease first. Deliberately narrow so that any other write failure propagates rather than being reported as a
    /// benign mint outcome. Mirrors the Dapper counterpart's <c>SqlException.Number is 2601 or 2627</c>.
    /// </summary>
    /// <remarks>
    /// Distinct from <c>EntityFrameworkCache.IsDuplicateKeyException</c>, which only recognises primary-key
    /// violations: the backstop here is a unique <em>index</em>, which reports different codes on SQL Server
    /// (2601 rather than 2627) and SQLite (2067 rather than 1555).
    /// </remarks>
    private static bool IsDuplicateKeyException(DbUpdateException e) => e.InnerException switch
    {
        MySqlConnector.MySqlException my => my.ErrorCode == MySqlConnector.MySqlErrorCode.DuplicateKeyEntry,
        Microsoft.Data.SqlClient.SqlException ms => ms.Errors
            .Cast<Microsoft.Data.SqlClient.SqlError>()
            .Any(error => error.Number is 2601 or 2627),
        Npgsql.PostgresException pg => pg.SqlState == "23505",
        Microsoft.Data.Sqlite.SqliteException lite => lite.SqliteErrorCode == 19
            && lite.SqliteExtendedErrorCode is 1555 or 2067,
        _ => false,
    };
}
