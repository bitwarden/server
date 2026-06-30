using AutoMapper;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CoreEntity = Bit.Pam.Entities.AccessRule;
using EfModel = Bit.Infrastructure.EntityFramework.Pam.Models.AccessRule;

#nullable enable

namespace Bit.Infrastructure.EntityFramework.Pam.Repositories;

public class AccessRuleRepository : Repository<CoreEntity, EfModel, Guid>, IAccessRuleRepository
{
    public AccessRuleRepository(IServiceScopeFactory serviceScopeFactory, IMapper mapper)
        : base(serviceScopeFactory, mapper, context => context.AccessRules)
    { }

    public async Task<ICollection<CoreEntity>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var rules = await dbContext.AccessRules
            .Where(p => p.OrganizationId == organizationId && p.DeletedDate == null)
            .AsNoTracking()
            .ToListAsync();
        return Mapper.Map<List<CoreEntity>>(rules);
    }

    public async Task<AccessRuleDetails?> GetDetailsByIdAsync(Guid id)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var rule = await dbContext.AccessRules
            .Where(p => p.Id == id && p.DeletedDate == null)
            .AsNoTracking()
            .FirstOrDefaultAsync();
        if (rule is null)
        {
            return null;
        }

        var details = Mapper.Map<AccessRuleDetails>(rule);
        details.CollectionIds = await dbContext.Collections
            .Where(c => c.AccessRuleId == id)
            .Select(c => c.Id)
            .ToListAsync();
        return details;
    }

    public async Task<ICollection<AccessRuleDetails>> GetManyDetailsByOrganizationIdAsync(Guid organizationId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var rules = await dbContext.AccessRules
            .Where(p => p.OrganizationId == organizationId && p.DeletedDate == null)
            .AsNoTracking()
            .ToListAsync();

        var collectionIdsByRule = (await dbContext.Collections
                .Where(c => c.OrganizationId == organizationId && c.AccessRuleId != null)
                .Select(c => new { AccessRuleId = c.AccessRuleId!.Value, CollectionId = c.Id })
                .ToListAsync())
            .GroupBy(r => r.AccessRuleId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.CollectionId).ToList());

        return rules
            .Select(rule =>
            {
                var details = Mapper.Map<AccessRuleDetails>(rule);
                if (collectionIdsByRule.TryGetValue(rule.Id, out var collectionIds))
                {
                    details.CollectionIds = collectionIds;
                }
                return details;
            })
            .ToList();
    }

    public override async Task DeleteAsync(CoreEntity accessRule)
    {
        // Mirrors the Dapper proc: delete is now a soft-delete (see SoftDeleteAsync).
        await SoftDeleteAsync(accessRule.Id, null, DateTime.UtcNow);
    }

    public async Task SoftDeleteAsync(Guid id, Guid? deletedBy, DateTime deletedDate)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var organizationId = await dbContext.AccessRules
            .Where(r => r.Id == id && r.DeletedDate == null)
            .Select(r => (Guid?)r.OrganizationId)
            .FirstOrDefaultAsync();
        if (organizationId is null)
        {
            // Already deleted or missing: idempotent no-op.
            return;
        }

        // Soft-delete: stamp the row but PRESERVE the Collection.AccessRuleId links so the rule_deleted audit event can
        // scope through them. Reads exclude DeletedDate != null, so the rule stops governing access.
        // RevisionDate is left untouched so the delete is not read as an edit (the rule_updated projection fires on
        // RevisionDate > CreationDate).
        await dbContext.AccessRules
            .Where(r => r.Id == id && r.DeletedDate == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.DeletedDate, deletedDate)
                .SetProperty(r => r.DeletedBy, deletedBy));

        await dbContext.UserBumpAccountRevisionDateByOrganizationIdAsync(organizationId.Value);
        await dbContext.SaveChangesAsync();
    }

    public async Task SetCollectionAssociationsAsync(Guid organizationId, Guid accessRuleId,
        IEnumerable<Guid> collectionIdsToAssign, IEnumerable<Guid> collectionIdsToClear)
    {
        var assignIds = collectionIdsToAssign.ToList();
        var clearIds = collectionIdsToClear.ToList();

        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var now = DateTime.UtcNow;

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        if (clearIds.Count > 0)
        {
            await dbContext.Collections
                .Where(c => c.OrganizationId == organizationId
                    && c.AccessRuleId == accessRuleId
                    && clearIds.Contains(c.Id))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.AccessRuleId, (Guid?)null)
                    .SetProperty(c => c.RevisionDate, now));
        }

        if (assignIds.Count > 0)
        {
            await dbContext.Collections
                .Where(c => c.OrganizationId == organizationId && assignIds.Contains(c.Id))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.AccessRuleId, accessRuleId)
                    .SetProperty(c => c.RevisionDate, now));
        }

        await dbContext.UserBumpAccountRevisionDateByOrganizationIdAsync(organizationId);
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
