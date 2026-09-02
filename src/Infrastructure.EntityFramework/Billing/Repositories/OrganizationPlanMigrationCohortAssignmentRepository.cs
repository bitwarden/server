using AutoMapper;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
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

    public async Task<int> GetCohortNonPendingAssignmentsCountAsync(Guid cohortId)
    {
        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var query =
            from a in dbContext.OrganizationPlanMigrationCohortAssignments
            join c in dbContext.OrganizationPlanMigrationCohorts on a.CohortId equals c.Id
            where a.CohortId == cohortId
                  && ((c.MigrationPathId != null && (a.ScheduledDate != null || a.MigratedDate != null))
                      || (c.MigrationPathId == null && a.ChurnDiscountAppliedDate != null))
            select a.Id;

        return await query.CountAsync();
    }

    public async Task<IReadOnlyList<CohortAssignmentExportRow>> GetExportRowsByCohortIdAsync(
        Guid cohortId, DateTime? afterCreationDate, Guid? afterId, int take)
    {
        if (afterCreationDate is null != (afterId is null))
        {
            throw new ArgumentException("afterCreationDate and afterId must both be set or both be null.");
        }

        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);

        var assignments = dbContext.OrganizationPlanMigrationCohortAssignments
            .Where(a => a.CohortId == cohortId);

        if (afterCreationDate != null)
        {
            assignments = assignments.Where(a =>
                a.CreationDate > afterCreationDate.Value
                || (a.CreationDate == afterCreationDate.Value
                    && a.Id > afterId!.Value));
        }

        return await assignments
            .OrderBy(a => a.CreationDate)
            .ThenBy(a => a.Id)
            .Take(take)
            .Select(a => new CohortAssignmentExportRow(
                a.Id,
                a.OrganizationId,
                a.Organization.Name,
                a.CreationDate,
                a.ScheduledDate,
                a.MigratedDate))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<CoreEntities.OrganizationPlanMigrationCohortAssignment>>
        GetSendInvoiceCandidatesInWindowAsync(int minDays, int maxDays)
    {
        if (minDays > maxDays)
        {
            throw new ArgumentException("minDays must be less than or equal to maxDays.");
        }

        using var scope = ServiceScopeFactory.CreateScope();
        var dbContext = GetDatabaseContext(scope);
        var now = DateTime.UtcNow;
        var results = await (
            from cma in dbContext.OrganizationPlanMigrationCohortAssignments.AsNoTracking()
            join c in dbContext.OrganizationPlanMigrationCohorts on cma.CohortId equals c.Id
            join o in dbContext.Organizations on cma.OrganizationId equals o.Id
            where c.IsActive
                  && cma.MigratedDate == null
                  && (cma.ScheduledDate == null || cma.RenewalNotificationSentDate == null)
                  && o.GatewayCustomerId != null
                  && o.GatewaySubscriptionId != null
                  && o.ExpirationDate != null
                  && o.ExpirationDate >= now.AddDays(minDays)
                  && o.ExpirationDate <= now.AddDays(maxDays)
            select cma
        ).ToListAsync();

        return Mapper.Map<List<CoreEntities.OrganizationPlanMigrationCohortAssignment>>(results);
    }

    public Task<CohortBulkAssignmentSummary> SyncManyAsync(
        IEnumerable<ResolvedCohortBulkAssignmentRow> rows) =>
        throw new NotSupportedException(
            "Bulk cohort assignment sync is only supported on Microsoft SQL Server.");
}
