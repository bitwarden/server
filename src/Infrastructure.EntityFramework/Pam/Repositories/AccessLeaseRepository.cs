using AutoMapper;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Enums;
using Bit.Pam.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CoreEntity = Bit.Pam.Entities.AccessLease;
using EfDecision = Bit.Infrastructure.EntityFramework.Pam.Models.AccessDecision;
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
            .Where(l => l.RequesterId == requesterId
                && l.CipherId == cipherId
                && l.Status == AccessLeaseStatus.Active
                && l.NotBefore <= now
                && l.NotAfter > now)
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
            .Where(l => l.RequesterId == requesterId
                && l.Status == AccessLeaseStatus.Active
                && l.NotBefore <= now
                && l.NotAfter > now)
            .OrderBy(l => l.NotAfter)
            .AsNoTracking()
            .ToListAsync();
        return Mapper.Map<List<CoreEntity>>(leases);
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
            .Where(l => ids.Contains(l.CollectionId)
                && l.Status == AccessLeaseStatus.Active
                && l.NotBefore <= now
                && l.NotAfter > now)
            .OrderBy(l => l.NotAfter)
            .AsNoTracking()
            .ToListAsync();
        return Mapper.Map<List<CoreEntity>>(leases);
    }

    public async Task<ICollection<CoreEntity>> GetManyEndedByCollectionIdsAsync(IEnumerable<Guid> collectionIds, DateTime since)
    {
        var ids = collectionIds.ToList();
        if (ids.Count == 0)
        {
            return new List<CoreEntity>();
        }

        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // A revoked/cancelled lease's end is its RevokedDate; an expired lease's end is its NotAfter.
        // `RevokedDate ?? NotAfter` is exactly that: RevokedDate is set only for Revoked/Cancelled leases.
        var leases = await dbContext.AccessLeases
            .Where(l => ids.Contains(l.CollectionId)
                && (l.Status == AccessLeaseStatus.Expired || l.Status == AccessLeaseStatus.Revoked || l.Status == AccessLeaseStatus.Cancelled)
                && (
                    ((l.Status == AccessLeaseStatus.Revoked || l.Status == AccessLeaseStatus.Cancelled) && l.RevokedDate >= since)
                    || (l.Status == AccessLeaseStatus.Expired && l.NotAfter >= since)
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

        // A Serializable transaction is the closest cross-provider approximation of the stored procedure's
        // UPDLOCK/HOLDLOCK range lock used for the per-cipher singleton guard: it keeps a concurrent same-cipher
        // activation from reading a pre-mint state. Unlike the SQL Server proc (which blocks a concurrent caller
        // until this transaction commits, then re-evaluates deterministically), a losing concurrent transaction here
        // fails at commit time with a provider-level serialization error instead of cleanly returning
        // SingleActiveLeaseConflict/PreconditionFailed -- which is why the caller of this method retries it.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

        try
        {
            if (enforceSingleActiveLease)
            {
                // The cipher is resolved from the request rather than the caller's copy of the lease, matching the
                // procedure's WHERE [CipherId] = (SELECT [CipherId] FROM [dbo].[AccessRequest] WHERE [Id] = ...).
                // Otherwise a lease whose CipherId disagrees with its AccessRequestId would be checked for
                // contention against the wrong cipher and could mint a second concurrent active lease.
                //
                // A request that does not exist yields no cipher and the guard is skipped, leaving the precondition
                // check below to report the failure -- exactly what the procedure's NULL scalar subquery does.
                var cipherId = await dbContext.AccessRequests
                    .Where(r => r.Id == lease.AccessRequestId)
                    .Select(r => (Guid?)r.CipherId)
                    .FirstOrDefaultAsync();

                if (cipherId is not null)
                {
                    var conflict = await dbContext.AccessLeases
                        .AnyAsync(l => l.CipherId == cipherId.Value
                            && l.Status == AccessLeaseStatus.Active
                            && l.NotBefore <= now
                            && l.NotAfter > now);
                    if (conflict)
                    {
                        await transaction.RollbackAsync();
                        return AccessLeaseMintOutcome.SingleActiveLeaseConflict;
                    }
                }
            }

            // Every application-level precondition is re-checked here so a concurrent activation cannot double-mint;
            // no matching request means a precondition no longer held and the caller decides how to surface that.
            var request = await dbContext.AccessRequests
                .Where(r => r.Id == lease.AccessRequestId
                    && r.RequesterId == lease.RequesterId
                    && r.Status == AccessRequestStatus.Approved
                    && r.NotBefore <= now
                    && r.NotAfter > now
                    && !dbContext.AccessLeases.Any(l => l.AccessRequestId == r.Id))
                .FirstOrDefaultAsync();

            if (request is null)
            {
                await transaction.RollbackAsync();
                return AccessLeaseMintOutcome.PreconditionFailed;
            }

            var leaseEntity = Mapper.Map<EfModel>(lease);
            leaseEntity.OrganizationId = request.OrganizationId;
            leaseEntity.CollectionId = request.CollectionId;
            leaseEntity.CipherId = request.CipherId;
            leaseEntity.RequesterId = request.RequesterId;
            leaseEntity.Status = AccessLeaseStatus.Active;
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

    public async Task RevokeAsync(CoreEntity lease, AccessLeaseStatus endStatus, AccessDecision auditDecision, DateTime now)
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
            .Where(l => l.Id == lease.Id && l.Status == AccessLeaseStatus.Active)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.Status, endStatus)
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
