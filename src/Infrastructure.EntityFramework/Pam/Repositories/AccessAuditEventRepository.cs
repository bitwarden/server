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
            Id = CoreHelpers.GenerateComb(),
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
        };

        dbContext.Add(row);
        await dbContext.SaveChangesAsync();
    }

    public async Task<ICollection<AccessAuditEvent>> GetManyByOrganizationIdAsync(
        Guid organizationId, DateTime since)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        // Self-contained rows, so this touches no other table -- the names were frozen at write time.
        return await dbContext.AccessAuditEvents
            .Where(e => e.OrganizationId == organizationId && e.OccurredAt >= since)
            .OrderByDescending(e => e.OccurredAt)
            .AsNoTracking()
            .Select(e => new AccessAuditEvent
            {
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
