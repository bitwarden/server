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
/// Derives from <see cref="BaseEntityFrameworkRepository"/> rather than <c>Repository&lt;,,&gt;</c>, since neither the
/// write payload nor the read model is an <c>ITableObject</c>.
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

        // Snapshotted at write time, as AccessAuditEvent_Create does with its LEFT JOINs.
        var actor = await ReadUserAsync(dbContext, auditEvent.ActorId);
        var requester = await ReadUserAsync(dbContext, auditEvent.RequesterId);

        var row = new EfModel
        {
            Id = CombGuid.Generate(),
            OrganizationId = auditEvent.OrganizationId,
            CorrelationId = auditEvent.CorrelationId,
            Kind = auditEvent.Kind,
            Phase = auditEvent.Phase,
            OccurredDate = auditEvent.OccurredDate,
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
            AccessConnectorId = auditEvent.AccessConnectorId,
            AccessConnectorName = auditEvent.AccessConnectorName,
            RotationConfigId = auditEvent.RotationConfigId,
            RotationJobId = auditEvent.RotationJobId,
            RotationSource = auditEvent.RotationSource,
            SyncState = auditEvent.SyncState,
        };

        dbContext.Add(row);
        await dbContext.SaveChangesAsync();
    }

    public async Task<ICollection<AccessAuditEvent>> GetPageByOrganizationIdAsync(
        Guid organizationId, AccessAuditTrailFilter filter)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var since = filter.Since;
        var until = filter.Until;

        var query = dbContext.AccessAuditEvents
            .Where(e => e.OrganizationId == organizationId && e.OccurredDate >= since && e.OccurredDate <= until);

        // The comparison has to match the ORDER BY below, or the cursor lands on a different boundary.
        if (filter.Before is { } before)
        {
            var beforeOccurredDate = before.OccurredDate;
            var beforeId = before.Id;
            query = query.Where(e =>
                e.OccurredDate < beforeOccurredDate
                || (e.OccurredDate == beforeOccurredDate && e.Id.CompareTo(beforeId) < 0));
        }

        // Collapses each action's pair to its further-along half, as NOT EXISTS because a GroupBy does not translate
        // on all three providers.
        query = query.Where(e => !dbContext.AccessAuditEvents.Any(p =>
            p.CorrelationId == e.CorrelationId
            && p.OrganizationId == organizationId
            && p.OccurredDate >= since
            && p.OccurredDate <= until
            && (p.Phase > e.Phase || (p.Phase == e.Phase && p.Id.CompareTo(e.Id) < 0))));

        if (filter.Kinds.Count > 0)
        {
            var kinds = filter.Kinds.ToList();
            query = query.Where(e => kinds.Contains(e.Kind));
        }

        if (filter.ActorIds.Count > 0 || filter.IncludeAutomatedActor)
        {
            var actorIds = filter.ActorIds.ToList();
            var includeAutomated = filter.IncludeAutomatedActor;
            query = query.Where(e =>
                (includeAutomated && e.ActorId == null)
                || (e.ActorId != null && actorIds.Contains(e.ActorId.Value)));
        }

        if (filter.RequesterIds.Count > 0)
        {
            var requesterIds = filter.RequesterIds.ToList();
            query = query.Where(e => e.RequesterId != null && requesterIds.Contains(e.RequesterId.Value));
        }

        if (filter.CipherIds.Count > 0 || filter.RuleIds.Count > 0)
        {
            var cipherIds = filter.CipherIds.ToList();
            var ruleIds = filter.RuleIds.ToList();
            query = query.Where(e =>
                (e.CipherId != null && cipherIds.Contains(e.CipherId.Value))
                || (e.AccessRuleId != null && ruleIds.Contains(e.AccessRuleId.Value)));
        }

        return await query
            .OrderByDescending(e => e.OccurredDate)
            .ThenByDescending(e => e.Id)
            .Take(filter.PageSize)
            .AsNoTracking()
            .Select(e => new AccessAuditEvent
            {
                Id = e.Id,
                Kind = e.Kind,
                Phase = e.Phase,
                CorrelationId = e.CorrelationId,
                OccurredDate = e.OccurredDate,
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
                AccessConnectorId = e.AccessConnectorId,
                AccessConnectorName = e.AccessConnectorName,
                RotationConfigId = e.RotationConfigId,
                RotationJobId = e.RotationJobId,
                RotationSource = e.RotationSource,
                SyncState = e.SyncState,
            })
            .ToListAsync();
    }

    public async Task<ICollection<AccessAuditItem>> GetItemsByOrganizationIdAsync(
        Guid organizationId, DateTime since, DateTime until)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var inRange = dbContext.AccessAuditEvents
            .Where(e => e.OrganizationId == organizationId && e.OccurredDate >= since && e.OccurredDate <= until)
            .AsNoTracking();

        // GroupBy + First where the procedure uses ROW_NUMBER, because that is what translates across the three
        // providers; the answer is the same.
        var ciphers = await inRange
            .Where(e => e.CipherId != null)
            .GroupBy(e => e.CipherId!.Value)
            .Select(group => new AccessAuditItem
            {
                CipherId = group.Key,
                CollectionId = group
                    .OrderByDescending(e => e.OccurredDate)
                    .ThenByDescending(e => e.Id)
                    .Select(e => e.CollectionId)
                    .First(),
            })
            .ToListAsync();

        var rules = await inRange
            .Where(e => e.AccessRuleId != null)
            .GroupBy(e => e.AccessRuleId!.Value)
            .Select(group => new AccessAuditItem
            {
                RuleId = group.Key,
                RuleName = group
                    .OrderByDescending(e => e.OccurredDate)
                    .ThenByDescending(e => e.Id)
                    .Select(e => e.RuleName)
                    .First(),
            })
            .ToListAsync();

        return [.. ciphers, .. rules];
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
