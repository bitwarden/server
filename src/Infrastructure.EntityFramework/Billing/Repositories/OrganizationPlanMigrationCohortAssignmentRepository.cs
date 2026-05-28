using AutoMapper;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CoreEntities = Bit.Core.Billing.Organizations.PlanMigration.Entities;
using EFOrganizationPlanMigrationCohortAssignment =
    Bit.Infrastructure.EntityFramework.Billing.Models.OrganizationPlanMigrationCohortAssignment;

namespace Bit.Infrastructure.EntityFramework.Billing.Repositories;

public class OrganizationPlanMigrationCohortAssignmentRepository(
    IMapper mapper,
    IServiceScopeFactory serviceScopeFactory)
    : Repository<CoreEntities.OrganizationPlanMigrationCohortAssignment,
        EFOrganizationPlanMigrationCohortAssignment, Guid>(
        serviceScopeFactory,
        mapper,
        context => context.OrganizationPlanMigrationCohortAssignments),
        IOrganizationPlanMigrationCohortAssignmentRepository
{
    public override async Task ReplaceAsync(CoreEntities.OrganizationPlanMigrationCohortAssignment obj)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var entity = await GetDbSet(dbContext).FindAsync(obj.Id);
        if (entity == null)
        {
            return;
        }

        var mappedEntity = Mapper.Map<EFOrganizationPlanMigrationCohortAssignment>(obj);
        dbContext.Entry(entity).CurrentValues.SetValues(mappedEntity);

        // Mirror the OrganizationPlanMigrationCohortAssignment_Update SP -- OrganizationId,
        // CohortId, and CreationDate are accepted but not assigned; the assignment-to-org and
        // assignment-to-cohort relationships cannot change after creation, and CreationDate is
        // immutable once the row is inserted.
        dbContext.Entry(entity).Property(a => a.OrganizationId).IsModified = false;
        dbContext.Entry(entity).Property(a => a.CohortId).IsModified = false;
        dbContext.Entry(entity).Property(a => a.CreationDate).IsModified = false;

        await dbContext.SaveChangesAsync();
    }

    public async Task<CoreEntities.OrganizationPlanMigrationCohortAssignment?> GetByOrganizationIdAsync(
        Guid organizationId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var result = await dbContext.OrganizationPlanMigrationCohortAssignments
            .FirstOrDefaultAsync(a => a.OrganizationId == organizationId);

        return Mapper.Map<CoreEntities.OrganizationPlanMigrationCohortAssignment>(result);
    }
}
