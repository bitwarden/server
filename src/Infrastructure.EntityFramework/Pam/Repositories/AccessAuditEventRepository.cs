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

    public async Task<ICollection<AccessAuditEvent>> GetPageByOrganizationIdAsync(
        Guid organizationId, AccessAuditTrailFilter filter)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var since = filter.Since;
        var until = filter.Until;

        // Rows are self-contained, so this touches no other table -- the names were frozen at write time.
        var query = dbContext.AccessAuditEvents
            .Where(e => e.OrganizationId == organizationId && e.OccurredDate >= since && e.OccurredDate <= until);

        // Resume where the previous page stopped. Paging is keyset rather than Skip: the store is append-only and read
        // newest first, so an offset would re-serve rows as events arrive. Keyed on (OccurredDate, Id) rather than
        // OccurredDate alone because an action writes its before/after halves at one instant, so a boundary landing
        // inside a group of events sharing a timestamp is ordinary here and a date-only key would drop every row tied
        // with it. The comparison has to match the ORDER BY below for the cursor to land on the same boundary the
        // previous page ended at.
        if (filter.Before is { } before)
        {
            var beforeOccurredDate = before.OccurredDate;
            var beforeId = before.Id;
            query = query.Where(e =>
                e.OccurredDate < beforeOccurredDate
                || (e.OccurredDate == beforeOccurredDate && e.Id.CompareTo(beforeId) < 0));
        }

        // Collapse each action's before/after pair (shared CorrelationId) into one row -- the Outcome when it landed,
        // otherwise the lone Attempt. Expressed as "no further-along half of this action exists" rather than as a
        // GroupBy, which is what the Dapper procedure's NOT EXISTS does and what translates to SQL here. Scoped to the
        // page's own range, so an action straddling a bound reads as in-doubt at that edge rather than disappearing.
        query = query.Where(e => !dbContext.AccessAuditEvents.Any(p =>
            p.CorrelationId == e.CorrelationId
            && p.OrganizationId == organizationId
            && p.OccurredDate >= since
            && p.OccurredDate <= until
            && (p.Phase > e.Phase || (p.Phase == e.Phase && p.Id.CompareTo(e.Id) < 0))));

        // The dimensions are applied AFTER the collapse, to the row that survived it, because the two halves of one
        // action need not agree: a refused activation writes its Attempt as LeaseActivated and its Outcome as
        // LeaseActivationRejected, so filtering before the collapse would answer "activated" with an action that was
        // turned down.
        if (filter.Kinds.Count > 0)
        {
            var kinds = filter.Kinds.ToList();
            query = query.Where(e => kinds.Contains(e.Kind));
        }

        // An actor selection unions the chosen identities with the automatic bucket, which has no id of its own.
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

        // The Item dimension is two columns, and they UNION rather than narrow: a rule-administration event names a
        // rule and no cipher, so one selection spanning both is asking for either, not for the empty intersection.
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
                DaemonId = e.DaemonId,
                DaemonName = e.DaemonName,
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

        // Grouped and ordered rather than aggregated so each subject carries its MOST RECENT context: a renamed rule
        // reads in the menu the way the newest rows read in the table, and a cipher's collection is the one it was
        // last gated through. Expressed as GroupBy + First rather than the procedure's ROW_NUMBER because that is what
        // translates across the three providers; the answer is the same.
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
