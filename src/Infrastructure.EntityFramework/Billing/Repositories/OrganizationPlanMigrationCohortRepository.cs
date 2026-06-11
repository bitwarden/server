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

        var query =
            from cohort in dbContext.OrganizationPlanMigrationCohorts
            where trimmedName == null || cohort.Name.ToLower().Contains(trimmedName)
            orderby cohort.CreationDate descending, cohort.Id ascending
            select new
            {
                Cohort = cohort,
                Pending = dbContext.OrganizationPlanMigrationCohortAssignments
                    .Count(a => a.CohortId == cohort.Id
                                && ((cohort.MigrationPathId != null && a.ScheduledDate == null)
                                    || (cohort.MigrationPathId == null && a.ChurnDiscountAppliedDate == null))),
                Scheduled = dbContext.OrganizationPlanMigrationCohortAssignments
                    .Count(a => a.CohortId == cohort.Id
                                && cohort.MigrationPathId != null
                                && a.ScheduledDate != null
                                && a.MigratedDate == null),
                Migrated = dbContext.OrganizationPlanMigrationCohortAssignments
                    .Count(a => a.CohortId == cohort.Id
                                && ((cohort.MigrationPathId != null && a.MigratedDate != null)
                                    || (cohort.MigrationPathId == null && a.ChurnDiscountAppliedDate != null))),
            };

        var rows = await query.Skip(skip).Take(take).ToListAsync();

        return rows.Select(r => new CohortListItem
        {
            Cohort = Mapper.Map<CoreEntities.OrganizationPlanMigrationCohort>(r.Cohort),
            Pending = r.Pending,
            Scheduled = r.Scheduled,
            Migrated = r.Migrated,
        });
    }

    public async Task<CoreEntities.OrganizationPlanMigrationCohort?> GetByNameAsync(string name)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var normalized = name.ToLower();

        var result = await dbContext.OrganizationPlanMigrationCohorts
            .FirstOrDefaultAsync(c => c.Name.ToLower() == normalized);

        return Mapper.Map<CoreEntities.OrganizationPlanMigrationCohort>(result);
    }
}
