using System.Text.Json;
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

        // Snapshot the display names into the row, the same way AccessAuditEvent_Create does with LEFT JOINs: resolve
        // them once here and freeze them, so a later delete or rename cannot change what this event says. A name stays
        // null where its id is null or the referenced row is gone. The rule name arrives on the payload rather than
        // being resolved -- a rule can be hard-deleted in the same action, so the command captures it beforehand.
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
            CipherName = await ReadCipherNameAsync(dbContext, auditEvent.CipherId),
            CollectionName = await ReadCollectionNameAsync(dbContext, auditEvent.CollectionId),
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

        // Self-contained rows, so this touches no other table -- the names were frozen at write time.
        var query = dbContext.AccessAuditEvents
            .Where(e => e.OrganizationId == organizationId && e.OccurredAt >= since && e.OccurredAt <= until);

        // Resume where the previous page stopped. Keyed on (OccurredAt, Id) rather than OccurredAt alone: an action
        // writes its before/after halves at one instant, so a boundary landing inside a group of events sharing a
        // timestamp is ordinary here, and a date-only key would drop every row tied with it.
        if (filter.BeforeOccurredAt is { } beforeOccurredAt)
        {
            var beforeId = filter.BeforeId ?? Guid.Empty;
            query = query.Where(e =>
                e.OccurredAt < beforeOccurredAt
                || (e.OccurredAt == beforeOccurredAt && e.Id.CompareTo(beforeId) < 0));
        }

        // Collapse each action's before/after pair (shared CorrelationId) into one row -- the Outcome when it landed,
        // otherwise the lone Attempt. Expressed as "no further-along half of this action exists" rather than as a
        // GroupBy, which is what the Dapper procedure's NOT EXISTS does and what translates to SQL here. Scoped to the
        // page's own range, so an action straddling a bound reads as in-doubt at that edge rather than disappearing.
        query = query.Where(e => !dbContext.AccessAuditEvents.Any(p =>
            p.CorrelationId == e.CorrelationId
            && p.OrganizationId == organizationId
            && p.OccurredAt >= since
            && p.OccurredAt <= until
            && (p.Phase > e.Phase || (p.Phase == e.Phase && p.Id.CompareTo(e.Id) < 0))));

        // The dimensions are applied AFTER the collapse, to the row that survived it, because the two halves of one
        // action need not agree: a refused activation writes its Attempt as LeaseActivated and its Outcome as
        // LeaseActivationRejected (ActivateAccessRequestCommand).
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
            .OrderByDescending(e => e.OccurredAt)
            .ThenByDescending(e => e.Id)
            .Take(filter.PageSize)
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
                CipherName = e.CipherName,
                CollectionName = e.CollectionName,
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
            .Where(e => e.OrganizationId == organizationId && e.OccurredAt >= since && e.OccurredAt <= until)
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
                    .OrderByDescending(e => e.OccurredAt)
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
                    .OrderByDescending(e => e.OccurredAt)
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

    private static async Task<string?> ReadCollectionNameAsync(DatabaseContext dbContext, Guid? collectionId)
    {
        if (!collectionId.HasValue)
        {
            return null;
        }

        return await dbContext.Collections
            .Where(c => c.Id == collectionId.Value)
            .Select(c => c.Name)
            .AsNoTracking()
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// The cipher's name lives inside its encrypted Data document. The stored procedure reads it with JSON_VALUE, which
    /// has no portable EF translation, so the document is fetched and the name read out here instead. A malformed or
    /// name-less document yields null rather than failing the audit write -- losing a display name must not cost the
    /// event.
    /// </summary>
    private static async Task<string?> ReadCipherNameAsync(DatabaseContext dbContext, Guid? cipherId)
    {
        if (!cipherId.HasValue)
        {
            return null;
        }

        var data = await dbContext.Ciphers
            .Where(c => c.Id == cipherId.Value)
            .Select(c => c.Data)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (string.IsNullOrEmpty(data))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(data);
            return document.RootElement.TryGetProperty("Name", out var name) && name.ValueKind == JsonValueKind.String
                ? name.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
