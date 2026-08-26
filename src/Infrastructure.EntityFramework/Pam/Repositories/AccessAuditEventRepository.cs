using AutoMapper;
using Bit.Core.Utilities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EfModel = Bit.Infrastructure.EntityFramework.Pam.Models.AccessAuditEvent;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Repositories;

/// <summary>
/// The EF counterpart of the Dapper audit store. Neither the write payload nor the read model is an
/// <c>ITableObject</c>, so this derives from <see cref="BaseEntityFrameworkRepository"/> rather than
/// <c>Repository&lt;,,&gt;</c> and maps both directions itself.
/// </summary>
public class AccessAuditEventRepository : BaseEntityFrameworkRepository, IAccessAuditEventRepository
{
    public AccessAuditEventRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper)
    { }

    public async Task CreateAsync(AccessAuditEventData auditEvent)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // Resolve the actor and requester names once and freeze them into the row, the same way
        // AccessAuditEvent_Create does with its LEFT JOINs, so a later delete or rename cannot change what this event
        // says. A name stays null where its id is null or the referenced row is gone.
        var actor = await ReadUserAsync(dbContext, auditEvent.ActorId);
        var requester = await ReadUserAsync(dbContext, auditEvent.RequesterId);

        var row = new EfModel
        {
            Id = CombGuid.Generate(),
            OrganizationId = auditEvent.OrganizationId,
            CorrelationId = auditEvent.CorrelationId,
            Kind = auditEvent.Kind,
            Phase = auditEvent.Phase,
            OccurredAt = auditEvent.OccurredAt,
            ActorId = auditEvent.ActorId,
            RequesterId = auditEvent.RequesterId,
            CollectionId = auditEvent.CollectionId,
            CipherId = auditEvent.CipherId,
            AccessRequestId = auditEvent.AccessRequestId,
            AccessLeaseId = auditEvent.AccessLeaseId,
            AccessRuleId = auditEvent.AccessRuleId,
            Detail = auditEvent.Detail,
            LeaseNotBefore = auditEvent.LeaseNotBefore,
            LeaseNotAfter = auditEvent.LeaseNotAfter,
            ActorName = actor?.Name,
            ActorEmail = actor?.Email,
            RequesterName = requester?.Name,
            RequesterEmail = requester?.Email,
            RuleName = auditEvent.RuleName,
            TargetSystemId = auditEvent.TargetSystemId,
            TargetSystemName = auditEvent.TargetSystemName,
            DaemonId = auditEvent.DaemonId,
            DaemonName = auditEvent.DaemonName,
            RotationConfigId = auditEvent.RotationConfigId,
            RotationJobId = auditEvent.RotationJobId,
            RotationSource = auditEvent.RotationSource,
            SyncState = auditEvent.SyncState,
        };

        dbContext.Add(row);
        await dbContext.SaveChangesAsync();
    }

    public async Task<ICollection<AccessAuditEvent>> GetManyByOrganizationIdAsync(
        Guid organizationId, DateTime since, AccessAuditEventCursor? before, int take)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // Rows are self-contained, so this touches no other table. Paging is keyset rather than Skip: the store is
        // append-only and read newest first, so an offset would re-serve rows as events arrive. Id breaks ties on
        // OccurredAt, which an action's Attempt and Outcome share, and the comparison has to match the ORDER BY below
        // for the cursor to land on the same boundary the previous page ended at.
        var query = dbContext.AccessAuditEvents
            .Where(e => e.OrganizationId == organizationId && e.OccurredAt >= since);

        if (before is not null)
        {
            var beforeOccurredAt = before.OccurredAt;
            var beforeId = before.Id;
            query = query.Where(e =>
                e.OccurredAt < beforeOccurredAt
                || (e.OccurredAt == beforeOccurredAt && e.Id.CompareTo(beforeId) < 0));
        }

        return await query
            .OrderByDescending(e => e.OccurredAt)
            .ThenByDescending(e => e.Id)
            .Take(take)
            .AsNoTracking()
            .Select(e => new AccessAuditEvent
            {
                Id = e.Id,
                Kind = e.Kind,
                Phase = e.Phase,
                CorrelationId = e.CorrelationId,
                OccurredAt = e.OccurredAt,
                OrganizationId = e.OrganizationId,
                ActorId = e.ActorId,
                RequesterId = e.RequesterId,
                CollectionId = e.CollectionId,
                CipherId = e.CipherId,
                AccessRequestId = e.AccessRequestId,
                AccessLeaseId = e.AccessLeaseId,
                AccessRuleId = e.AccessRuleId,
                Detail = e.Detail,
                LeaseNotBefore = e.LeaseNotBefore,
                LeaseNotAfter = e.LeaseNotAfter,
                ActorName = e.ActorName,
                ActorEmail = e.ActorEmail,
                RequesterName = e.RequesterName,
                RequesterEmail = e.RequesterEmail,
                RuleName = e.RuleName,
                TargetSystemId = e.TargetSystemId,
                TargetSystemName = e.TargetSystemName,
                DaemonId = e.DaemonId,
                DaemonName = e.DaemonName,
                RotationConfigId = e.RotationConfigId,
                RotationJobId = e.RotationJobId,
                RotationSource = e.RotationSource,
                SyncState = e.SyncState,
            })
            .ToListAsync();
    }

    private static async Task<(string? Name, string? Email)?> ReadUserAsync(DatabaseContext dbContext, Guid? userId)
    {
        if (!userId.HasValue)
        {
            return null;
        }

        var user = await dbContext.Users
            .Where(u => u.Id == userId.Value)
            .Select(u => new { u.Name, u.Email })
            .AsNoTracking()
            .FirstOrDefaultAsync();

        return user is null ? null : (user.Name, user.Email);
    }
}
