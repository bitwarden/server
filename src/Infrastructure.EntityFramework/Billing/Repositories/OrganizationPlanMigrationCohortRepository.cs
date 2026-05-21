using AutoMapper;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CoreEntities = Bit.Core.Billing.Organizations.PlanMigration.Entities;
using EFOrganizationPlanMigrationCohort =
    Bit.Infrastructure.EntityFramework.Billing.Models.OrganizationPlanMigrationCohort;

namespace Bit.Infrastructure.EntityFramework.Billing.Repositories;

public class OrganizationPlanMigrationCohortRepository(
    IMapper mapper,
    IServiceScopeFactory serviceScopeFactory)
    : Repository<CoreEntities.OrganizationPlanMigrationCohort, EFOrganizationPlanMigrationCohort, Guid>(
        serviceScopeFactory,
        mapper,
        context => context.OrganizationPlanMigrationCohorts),
        IOrganizationPlanMigrationCohortRepository
{
    public override async Task ReplaceAsync(CoreEntities.OrganizationPlanMigrationCohort obj)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var entity = await GetDbSet(dbContext).FindAsync(obj.Id);
        if (entity == null)
        {
            return;
        }

        var mappedEntity = Mapper.Map<EFOrganizationPlanMigrationCohort>(obj);
        dbContext.Entry(entity).CurrentValues.SetValues(mappedEntity);

        // Mirror the OrganizationPlanMigrationCohort_Update SP -- CreationDate is accepted but
        // not assigned; it is immutable once the row is inserted.
        dbContext.Entry(entity).Property(c => c.CreationDate).IsModified = false;

        await dbContext.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<CoreEntities.OrganizationPlanMigrationCohort>> GetManyAsync()
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var results = await dbContext.OrganizationPlanMigrationCohorts
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync();

        return Mapper.Map<List<CoreEntities.OrganizationPlanMigrationCohort>>(results);
    }

    public async Task<IEnumerable<CohortListItem>> SearchWithCountsAsync(
        string? name,
        int skip,
        int take)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var trimmedName = string.IsNullOrWhiteSpace(name) ? null : name.ToLower();

        var rows = await dbContext.OrganizationPlanMigrationCohorts
            .Where(c => trimmedName == null || c.Name.ToLower().Contains(trimmedName))
            .OrderByDescending(c => c.CreationDate)
            .ThenBy(c => c.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return rows.Select(cohort => new CohortListItem
        {
            Cohort = Mapper.Map<CoreEntities.OrganizationPlanMigrationCohort>(cohort),
        });
    }
}
