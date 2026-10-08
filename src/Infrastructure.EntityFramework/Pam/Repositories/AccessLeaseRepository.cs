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
    /// The live-lease predicate every EF authorization read composes. The stored procedures carry the same predicate
    /// and must not drift from it.
    /// </summary>
    private static System.Linq.Expressions.Expression<Func<EfModel, bool>> LiveAt(DateTime now)
        => l => l.Action == AccessLeaseAction.None && l.NotBefore <= now && l.NotAfter > now;

    public async Task<CoreEntity?> GetByAccessRequestIdAsync(Guid accessRequestId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // A request produces at most one lease ([IX_AccessLease_AccessRequestId] is unique), so the ordering is only a
        // fallback.
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

        // Latest-ending across all members, since the singleton guard blocks until the last live lease frees the slot.
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

        // An early end (Revoked, Cancelled) ends at RevokedDate, a natural close at NotAfter, as in the procedure.
        var leases = await dbContext.AccessLeases
            .Where(l => ids.Contains(l.CollectionId)
                && (
                    ((l.Action == AccessLeaseAction.Revoked || l.Action == AccessLeaseAction.Cancelled) && l.RevokedDate >= since)
                    || (l.Action == AccessLeaseAction.None && l.NotAfter <= now && l.NotAfter >= since)
                ))
            .OrderByDescending(l => l.RevokedDate ?? l.NotAfter)
            .AsNoTracking()
            .ToListAsync();

        return Mapper.Map<List<CoreEntity>>(leases);
    }

    /// <remarks>
    /// Retried on a fresh transaction after a serialization failure or deadlock, a bounded number of times.
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

        // Serializable only for the per-cipher guard, matching the procedure's range lock; the claim below protects
        // every other write.
        var isolation = enforceSingleActiveLease
            ? System.Data.IsolationLevel.Serializable
            : System.Data.IsolationLevel.ReadCommitted;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(isolation);

        try
        {
            // Claims the request row first, as the procedure does, closing a write-skew race with retraction that SSI
            // misses because retraction is not Serializable. Must stay ahead of the singleton guard, since the reverse
            // lock order deadlocks.
            var claimed = await dbContext.AccessRequests
                .Where(r => r.Id == lease.AccessRequestId
                    && r.RequesterId == lease.RequesterId
                    && r.Action == AccessRequestAction.Approved
                    // An extension applies in place on approval and never mints a lease.
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

            // The claim holds the row, so this read cannot go stale.
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
            // Starts at activation, never backdated, as in the procedure; the end stays the request's, so a late
            // activation shortens the lease.
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
            // Unique-index backstop ([IX_AccessLease_AccessRequestId]): a concurrent activation won, so the caller
            // re-reads the winner. Any other failure propagates, since this path grants access to vault data.
            await transaction.RollbackAsync();
            return AccessLeaseMintOutcome.PreconditionFailed;
        }
    }

    public async Task RevokeAsync(CoreEntity lease, AccessLeaseAction endAction, AccessDecision auditDecision, DateTime now)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        // The reason has no column, so it is kept as a human Deny decision on the originating request. The request id
        // comes from the lease row, not the caller's copy, as in the procedure's OUTPUT clause.
        var accessRequestId = await dbContext.AccessLeases
            .Where(l => l.Id == lease.Id)
            .Select(l => l.AccessRequestId)
            .FirstOrDefaultAsync();

        // The decision is recorded only on an actual transition, so a repeat or losing revoke appends no Deny.
        var rowsAffected = await dbContext.AccessLeases
            .Where(l => l.Id == lease.Id && l.Action == AccessLeaseAction.None && l.NotAfter > now)
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
    /// True when the provider aborted the transaction as a serialization failure or deadlock victim. The transaction
    /// is gone, so the only recovery is to run the whole attempt again.
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
    /// True when a duplicate key tripped the [IX_AccessLease_AccessRequestId] backstop. Unlike
    /// <c>EntityFrameworkCache.IsDuplicateKeyException</c> it recognizes unique-index codes too: 2601 on SQL Server,
    /// 2067 on SQLite.
    /// </summary>
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
