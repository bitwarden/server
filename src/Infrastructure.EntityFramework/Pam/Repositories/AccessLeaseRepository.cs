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
    /// The live-lease predicate as a shared, EF-translatable expression: no early end recorded and the window open
    /// at <paramref name="now"/> (authorization checks both window ends -- stricter than display, where NotBefore is
    /// vacuous by the mint invariant). Every EF read that means "currently authorizes access" composes this rather
    /// than respelling the three clauses; the stored procedures carry the same predicate in SQL and must not drift.
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

        // Latest-ending, across all members: the singleton guard blocks while any in-window lease exists, so the slot
        // frees when the last one does. Cipher-scoped to match that guard, which ignores CollectionId.
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

        // A revoked/cancelled lease's end is its RevokedDate; an expired lease's end is its NotAfter.
        // `RevokedDate ?? NotAfter` is exactly that: RevokedDate is set only for ended-early leases.
        //
        // Ended-ness has to be derived: the recorded action only ever says how a lease was ended early, so a lease
        // whose window merely closed carries None forever and only the clock can call it Expired. The filter
        // composes the action with a plain clock comparison, exactly like AccessLease_ReadManyEndedByCollectionIds;
        // the returned entities expose the stored facts only, and callers derive the status via
        // AccessStatusDerivation.ComputeLeaseStatus.
        var leases = await dbContext.AccessLeases
            .Where(l => ids.Contains(l.CollectionId)
                && (
                    // Ended early (Revoked, Cancelled): its end is RevokedDate, whatever its window says.
                    ((l.Action == AccessLeaseAction.Revoked || l.Action == AccessLeaseAction.Cancelled) && l.RevokedDate >= since)
                    // Window closed on its own: its end is NotAfter. Byte 1 (the retired stored Expired) is
                    // deliberately NOT matched: nothing ever wrote it, and ComputeLeaseStatus has no arm for it, so
                    // reading such a stray row would fail the whole endpoint. Not read means not derived -- it
                    // simply stays invisible. Mirrors AccessLease_ReadManyEndedByCollectionIds.
                    || (l.Action == AccessLeaseAction.None && l.NotAfter <= now && l.NotAfter >= since)
                ))
            .OrderByDescending(l => l.RevokedDate ?? l.NotAfter)
            .AsNoTracking()
            .ToListAsync();

        return Mapper.Map<List<CoreEntity>>(leases);
    }

    /// <remarks>
    /// A provider serialization failure is retried rather than surfaced. The per-cipher guard below reads a
    /// predicate rather than a row, so under Serializable isolation this transaction is a candidate for abort
    /// whenever any other transaction inserts a lease before it commits -- including one that grants access to an
    /// unrelated cipher. A losing attempt therefore runs again on a fresh transaction and re-reads the state its
    /// guard needs, arriving at the same deterministic outcome the stored procedure's UPDLOCK/HOLDLOCK blocks for.
    /// Retries are bounded: a failure that outlives them propagates, because on a path that grants access to Vault
    /// Data an unresolved persistence failure must not be reported as a benign mint outcome.
    ///
    /// That only applies when <paramref name="enforceSingleActiveLease"/> asks for the guard, which is the sole
    /// reason this path ever needs Serializable -- see <see cref="MintFromApprovedRequestAsync"/>.
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

        // Serializable is reserved for the per-cipher singleton guard, which is the only thing here that needs it: it
        // is an anti-join over a *set* ("no other active lease for this cipher"), so there is no row whose lock could
        // stand in for it, and Serializable is the closest cross-provider approximation of the stored procedure's
        // UPDLOCK/HOLDLOCK range lock. Unlike the SQL Server proc (which blocks a concurrent caller until this
        // transaction commits, then re-evaluates deterministically), a losing concurrent transaction here fails at
        // commit time with a provider-level serialization error instead of cleanly returning
        // SingleActiveLeaseConflict/PreconditionFailed -- which is why the caller of this method retries it.
        //
        // Everything else is protected by the claim below rather than by isolation: it is a real row lock plus a CAS,
        // which holds at every level. Taking Serializable when no guard is asked for bought nothing and cost
        // plenty -- on PostgreSQL it enrolled every activation in SSI, so unrelated concurrent writers (notably
        // CreateApprovedExtensionAsync, which is Serializable and does *not* retry) were aborted with 40001 by
        // activations they had no true conflict with. This also restores parity with
        // [AccessLease_CreateFromApprovedRequest], which likewise runs at the ambient ReadCommitted and reaches for
        // its UPDLOCK/HOLDLOCK range lock only under @EnforceSingleActiveLease.
        var isolation = enforceSingleActiveLease
            ? System.Data.IsolationLevel.Serializable
            : System.Data.IsolationLevel.ReadCommitted;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(isolation);

        try
        {
            // Claim the request row, then mint. Activation used to read AccessRequest and write only AccessLease
            // while a retraction reads AccessLease and writes only AccessRequest -- write skew across two tables,
            // where neither side writes what the other reads, so both could commit and leave a Cancelled/Denied
            // request holding a live lease. Access is governed by the lease alone once it exists, so that
            // combination hands the requester the credential their request was withdrawn from.
            //
            // This UPDATE closes it from the activation side. It is semantically a no-op -- an activated request
            // stays Approved, there is no 'activated' action -- but it makes activation a *writer* of the row the
            // retraction paths write, so the two serialize on that row's write lock, held until this transaction
            // commits. The other half is in AccessRequestRepository's retraction paths, which claim the same row
            // before they probe for a lease. Mirrors the claiming UPDATE in
            // [AccessLease_CreateFromApprovedRequest], including its position ahead of the singleton guard: the
            // retraction paths lock AccessRequest and then read AccessLease, so taking the guard first would invert
            // the two operations' lock order and make them deadlock.
            //
            // Serializable alone did not cover this. It is the mint's isolation, but the retraction paths run at the
            // provider default, and PostgreSQL's SSI only detects a read/write dependency cycle when every
            // transaction in it is Serializable -- so the cycle this pair forms went unnoticed.
            //
            // Every application-level precondition is re-checked here, so the claim and the guard are one statement
            // and one CAS: a retraction that committed first has already moved Action off Approved, which is a clean
            // zero-row outcome rather than a lost update.
            var claimed = await dbContext.AccessRequests
                .Where(r => r.Id == lease.AccessRequestId
                    && r.RequesterId == lease.RequesterId
                    && r.Action == AccessRequestAction.Approved
                    // An extension applied in place when it was approved and never mints a lease of its own; it stays
                    // Approved with no produced lease, so every other precondition here would pass for it.
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

            // The claim proved the row exists and matches every precondition, and holds it for the rest of this
            // transaction, so this read cannot miss and its values cannot go stale under us. It supplies the columns
            // the lease is minted from, which are taken from the request rather than the caller's copy of the lease.
            var request = await dbContext.AccessRequests
                .AsNoTracking()
                .FirstAsync(r => r.Id == lease.AccessRequestId);

            if (enforceSingleActiveLease)
            {
                // The cipher is resolved from the request rather than the caller's copy of the lease, matching the
                // procedure's WHERE [CipherId] = (SELECT [CipherId] FROM [dbo].[AccessRequest] WHERE [Id] = ...).
                // Otherwise a lease whose CipherId disagrees with its AccessRequestId would be checked for
                // contention against the wrong cipher and could mint a second concurrent active lease.
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
            leaseEntity.NotBefore = request.NotBefore;
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

        // Expiry is derived rather than stored (a lease whose window closed on its own keeps Action = None forever),
        // so there is no status flip to mark a lease as processed. The PamLeaseExpirySweep journal is the once-only
        // arbiter instead: a lease is returned only by the run that journals it. No stronger isolation is needed --
        // if two sweeps race past the read, the journal's primary key fails the loser's SaveChanges before it can
        // return anything, which keeps at-most-once without serializable range locks across the whole lease table.
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
