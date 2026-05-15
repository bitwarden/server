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
