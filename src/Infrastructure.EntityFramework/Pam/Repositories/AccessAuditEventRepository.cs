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

        // Snapshots display names into the row (as AccessAuditEvent_Create's LEFT JOINs do): resolved once and frozen,
        // so a later delete or rename can't change what this event says. The rule name arrives pre-resolved on the
        // payload since a rule can be hard-deleted in the same action.
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

        // Resumes on (OccurredAt, Id), not OccurredAt alone, since a boundary landing inside a same-instant group is
        // ordinary here and a date-only key would drop rows tied with it.
        if (filter.BeforeOccurredAt is { } beforeOccurredAt)
        {
            var beforeId = filter.BeforeId ?? Guid.Empty;
            query = query.Where(e =>
                e.OccurredAt < beforeOccurredAt
                || (e.OccurredAt == beforeOccurredAt && e.Id.CompareTo(beforeId) < 0));
        }

        // Collapses each before/after pair (shared CorrelationId) into one row: Outcome if landed, else the lone
        // Attempt. Expressed as "no further-along half exists" (translates to SQL as NOT EXISTS), scoped to the
        // page's own range.
        query = query.Where(e => !dbContext.AccessAuditEvents.Any(p =>
            p.CorrelationId == e.CorrelationId
            && p.OrganizationId == organizationId
            && p.OccurredAt >= since
            && p.OccurredAt <= until
            && (p.Phase > e.Phase || (p.Phase == e.Phase && p.Id.CompareTo(e.Id) < 0))));

        // Applied after the collapse, since an action's two halves can disagree (e.g. Attempt LeaseActivated, Outcome
        // LeaseActivationRejected).
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

        // The Item dimension is two columns that UNION rather than narrow: a rule-administration event has no cipher.
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

        // Grouped and ordered, not aggregated, so each subject carries its most recent context (current rule name,
        // last-gated collection). Uses GroupBy + First rather than the procedure's ROW_NUMBER since that's what
        // translates across all three providers.
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
