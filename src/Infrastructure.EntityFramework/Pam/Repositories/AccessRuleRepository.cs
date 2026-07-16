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
            .Where(p => p.OrganizationId == organizationId)
            .AsNoTracking()
            .ToListAsync();
        return Mapper.Map<List<CoreEntity>>(rules);
    }

    public async Task<AccessRuleDetails?> GetDetailsByIdAsync(Guid id)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var rule = await dbContext.AccessRules
            .Where(p => p.Id == id)
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
            .Where(p => p.OrganizationId == organizationId)
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
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        // Clear the collection links first (the FK Collection.AccessRuleId -> AccessRule is ON DELETE NO ACTION), then
        // remove the rule. A cleared collection is simply ungoverned; the RuleDeleted audit event already carries the
        // rule's name, so the row need not survive.
        await dbContext.Collections
            .Where(c => c.AccessRuleId == accessRule.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.AccessRuleId, (Guid?)null)
                .SetProperty(c => c.RevisionDate, DateTime.UtcNow));

        await dbContext.AccessRules
            .Where(r => r.Id == accessRule.Id)
            .ExecuteDeleteAsync();

        await dbContext.UserBumpAccountRevisionDateByOrganizationIdAsync(accessRule.OrganizationId);
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
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
