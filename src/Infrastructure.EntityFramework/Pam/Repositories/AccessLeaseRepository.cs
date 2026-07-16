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

    public async Task<AccessLeaseMintOutcome> CreateFromApprovedRequestAsync(CoreEntity lease, DateTime now,
        bool enforceSingleActiveLease)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // A Serializable transaction is the closest cross-provider approximation of the stored procedure's
        // UPDLOCK/HOLDLOCK range lock used for the per-cipher singleton guard: it keeps a concurrent same-cipher
        // activation from reading a pre-mint state. Unlike the SQL Server proc (which blocks a concurrent caller
        // until this transaction commits, then re-evaluates deterministically), a losing concurrent transaction here
        // may instead fail at commit time with a provider-level serialization error rather than cleanly returning
        // SingleActiveLeaseConflict/PreconditionFailed -- callers should be prepared to treat such an exception as a
        // conflict and re-read.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

        try
        {
            if (enforceSingleActiveLease)
            {
                var conflict = await dbContext.AccessLeases
                    .AnyAsync(l => l.CipherId == lease.CipherId
                        && l.Status == AccessLeaseStatus.Active
                        && l.NotBefore <= now
                        && l.NotAfter > now);
                if (conflict)
                {
                    await transaction.RollbackAsync();
                    return AccessLeaseMintOutcome.SingleActiveLeaseConflict;
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
        catch (DbUpdateException)
        {
            // The unique-index backstop ([IX_AccessLease_AccessRequestId]): a concurrent activation won the race
            // after our application-level precondition check passed. Same outcome as the guard catching it -- the
            // caller re-reads the winner.
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
        // lease's originating request, keeping the audit trail without a schema change.
        await dbContext.AccessLeases
            .Where(l => l.Id == lease.Id && l.Status == AccessLeaseStatus.Active)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.Status, endStatus)
                .SetProperty(l => l.RevokedDate, now)
                .SetProperty(l => l.RevokedBy, auditDecision.ApproverId));

        var decisionEntity = Mapper.Map<EfDecision>(auditDecision);
        decisionEntity.AccessRequestId = lease.AccessRequestId;
        decisionEntity.DeciderKind = AccessDeciderKind.Human;
        decisionEntity.ConditionKind = null;
        decisionEntity.Verdict = AccessDecisionVerdict.Deny;
        decisionEntity.EvaluationContext = null;
        decisionEntity.CreationDate = now;

        await dbContext.AccessDecisions.AddAsync(decisionEntity);
        await dbContext.SaveChangesAsync();

        await transaction.CommitAsync();
    }
}
