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

        var stored = await dbContext.AccessRules
            .Where(r => r.Id == accessRule.Id)
            .Select(r => new { r.OrganizationId, r.Enabled })
            .FirstOrDefaultAsync();

        // Detach collections first: FK Collection.AccessRuleId does not cascade (RESTRICT here, NO ACTION on SQL
        // Server), so the delete fails while any collection points at the rule.
        var governed = dbContext.Collections.Where(c => c.AccessRuleId == accessRule.Id);
        var clearedCollectionIds = await governed.Select(c => c.Id).ToListAsync();
        await governed
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.AccessRuleId, (Guid?)null)
                .SetProperty(c => c.RevisionDate, DateTime.UtcNow));

        // Detach the requests that pinned this rule, since FK_AccessRequest_AccessRule does not cascade either. RuleId
        // is provenance, not authority, so clearing it changes no grant.
        await dbContext.AccessRequests
            .Where(r => r.RuleId == accessRule.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.RuleId, (Guid?)null));

        await dbContext.AccessRules
            .Where(r => r.Id == accessRule.Id)
            .ExecuteDeleteAsync();

        // Only an enabled rule gated its collections, so only then do their members see a change.
        if (stored is { Enabled: true } && clearedCollectionIds.Count > 0)
        {
            await dbContext.UserBumpAccountRevisionDateByCollectionIdsAsync(clearedCollectionIds, stored.OrganizationId);
        }

        await transaction.CommitAsync();
    }

    /// <remarks>
    /// Members sync whether each collection is governed by an enabled rule, so toggling <c>Enabled</c> makes the
    /// members of every collection the rule governs resync. Any other field change is invisible to sync. The toggle
    /// is detected by the conditional update itself, so of two saves racing on the same toggle only one bumps.
    /// </remarks>
    public override async Task ReplaceAsync(CoreEntity accessRule)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        var toggled = await dbContext.AccessRules
            .Where(r => r.Id == accessRule.Id && r.Enabled != accessRule.Enabled)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Enabled, accessRule.Enabled)) > 0;

        var entity = await dbContext.AccessRules.FindAsync(accessRule.Id);
        if (entity is null)
        {
            return;
        }

        dbContext.Entry(entity).CurrentValues.SetValues(Mapper.Map<EfModel>(accessRule));
        await dbContext.SaveChangesAsync();

        if (toggled)
        {
            var collectionIds = await dbContext.Collections
                .Where(c => c.AccessRuleId == accessRule.Id)
                .Select(c => c.Id)
                .ToListAsync();
            await dbContext.UserBumpAccountRevisionDateByCollectionIdsAsync(collectionIds, accessRule.OrganizationId);
        }

        await transaction.CommitAsync();
    }
}
