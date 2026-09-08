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

        // Clear the collection links before deleting the rule: the FK Collection.AccessRuleId -> AccessRule does
        // not cascade (RESTRICT here, NO ACTION on SQL Server), so the delete fails while any collection still
        // points at it.
        await dbContext.Collections
            .Where(c => c.AccessRuleId == accessRule.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.AccessRuleId, (Guid?)null)
                .SetProperty(c => c.RevisionDate, DateTime.UtcNow));

        // Detach the requests that pinned this rule for the same reason: FK_AccessRequest_AccessRule does not
        // cascade either, so a request recording this rule as its governing rule would block the delete. RuleId is
        // provenance rather than authority, and is already nullable for requests never gated through a stored rule.
        await dbContext.AccessRequests
            .Where(r => r.RuleId == accessRule.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.RuleId, (Guid?)null));

        await dbContext.AccessRules
            .Where(r => r.Id == accessRule.Id)
            .ExecuteDeleteAsync();

        await dbContext.UserBumpAccountRevisionDateByOrganizationIdAsync(accessRule.OrganizationId);
        await dbContext.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
