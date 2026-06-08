using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Billing.PlanMigration;

public class CohortBulkAssignmentRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task GetPlanTypesByOrganizationIdsAsync_ReturnsIdAndPlanType_ForExistingOrgsOnly(
        IOrganizationRepository organizationRepository)
    {
        var org = await organizationRepository.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = "billing@example.com",
            Plan = "Enterprise (Annually) 2020",
            PlanType = PlanType.EnterpriseAnnually2020,
        });

        var missingId = Guid.NewGuid();

        var results = await organizationRepository.GetPlanTypesByOrganizationIdsAsync([org.Id, missingId]);

        var row = Assert.Single(results);
        Assert.Equal(org.Id, row.OrganizationId);
        Assert.Equal(PlanType.EnterpriseAnnually2020, row.PlanType);
    }

    [DatabaseTheory, DatabaseData]
    public async Task GetManyByNamesAsync_MatchesCaseInsensitively_ReturnsOnlyRequested(
        IOrganizationPlanMigrationCohortRepository cohortRepository)
    {
        var a = await cohortRepository.CreateAsync(new OrganizationPlanMigrationCohort
        {
            Name = $"A1 (a) {Guid.NewGuid()}",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
        });
        await cohortRepository.CreateAsync(new OrganizationPlanMigrationCohort
        {
            Name = $"Other {Guid.NewGuid()}",
            MigrationPathId = MigrationPathId.Enterprise2020MonthlyToCurrent,
        });

        var results = await cohortRepository.GetManyByNamesAsync([a.Name.ToUpperInvariant()]);

        var match = Assert.Single(results);
        Assert.Equal(a.Id, match.Id);
    }

    [DatabaseTheory, DatabaseData]
    public async Task SyncManyAsync_InsertsUpdatesAndUnassigns(
        Database database,
        IOrganizationRepository organizationRepository,
        IOrganizationPlanMigrationCohortRepository cohortRepository,
        IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository)
    {
        // Bulk sync is a SqlServer/Dapper-only (OPENJSON MERGE) operation; EF providers
        // intentionally throw. Assert that contract on EF, run the real sync on SqlServer.
        if (database.Type != SupportedDatabaseProviders.SqlServer || database.UseEf)
        {
            await Assert.ThrowsAsync<NotSupportedException>(() =>
                assignmentRepository.SyncManyAsync(
                    [new ResolvedCohortBulkAssignmentRow(Guid.NewGuid(), null)]));
            return;
        }

        var orgToInsert = await CreateOrgAsync(organizationRepository);
        var orgToMove = await CreateOrgAsync(organizationRepository);
        var orgToUnassign = await CreateOrgAsync(organizationRepository);
        var cohortA = await cohortRepository.CreateAsync(new OrganizationPlanMigrationCohort
        {
            Name = $"Cohort A {Guid.NewGuid()}",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
        });
        var cohortB = await cohortRepository.CreateAsync(new OrganizationPlanMigrationCohort
        {
            Name = $"Cohort B {Guid.NewGuid()}",
            MigrationPathId = MigrationPathId.Enterprise2020MonthlyToCurrent,
        });

        // Seed: orgToMove is on cohortA (CSV moves it to cohortB → UPDATE);
        // orgToUnassign is on cohortA (sentinel row removes it → DELETE).
        await assignmentRepository.CreateAsync(new OrganizationPlanMigrationCohortAssignment
        {
            OrganizationId = orgToMove.Id,
            CohortId = cohortA.Id,
        });
        await assignmentRepository.CreateAsync(new OrganizationPlanMigrationCohortAssignment
        {
            OrganizationId = orgToUnassign.Id,
            CohortId = cohortA.Id,
        });

        var rows = new[]
        {
            new ResolvedCohortBulkAssignmentRow(orgToInsert.Id, cohortA.Id),    // insert
            new ResolvedCohortBulkAssignmentRow(orgToMove.Id, cohortB.Id),      // update (move A -> B)
            new ResolvedCohortBulkAssignmentRow(orgToUnassign.Id, null),        // unassign
        };

        var result = await assignmentRepository.SyncManyAsync(rows);

        Assert.Equal(1, result.Inserted);
        Assert.Equal(1, result.Updated);
        Assert.Equal(1, result.Unassigned);
        Assert.NotNull(await assignmentRepository.GetByOrganizationIdAsync(orgToInsert.Id));
        var moved = await assignmentRepository.GetByOrganizationIdAsync(orgToMove.Id);
        Assert.NotNull(moved);
        Assert.Equal(cohortB.Id, moved!.CohortId);
        Assert.Null(await assignmentRepository.GetByOrganizationIdAsync(orgToUnassign.Id));
    }

    [DatabaseTheory, DatabaseData]
    public async Task SyncManyAsync_SkipsLockedAssignments_AndCountsOnlyChangedSkips(
        Database database,
        IOrganizationRepository organizationRepository,
        IOrganizationPlanMigrationCohortRepository cohortRepository,
        IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository)
    {
        // Bulk sync is SqlServer/Dapper-only; EF providers throw. Skip the behavioral
        // assertions on every provider except real SqlServer.
        if (database.Type != SupportedDatabaseProviders.SqlServer || database.UseEf)
        {
            return;
        }

        // A migration cohort (MigrationPathId != null) and a churn cohort (MigrationPathId == null).
        var migrationCohort = await cohortRepository.CreateAsync(new OrganizationPlanMigrationCohort
        {
            Name = $"Migration {Guid.NewGuid()}",
            MigrationPathId = MigrationPathId.Enterprise2020AnnualToCurrent,
        });
        var churnCohort = await cohortRepository.CreateAsync(new OrganizationPlanMigrationCohort
        {
            Name = $"Churn {Guid.NewGuid()}",
            MigrationPathId = null,
        });
        var destinationCohort = await cohortRepository.CreateAsync(new OrganizationPlanMigrationCohort
        {
            Name = $"Destination {Guid.NewGuid()}",
            MigrationPathId = MigrationPathId.Enterprise2020MonthlyToCurrent,
        });

        var orgScheduled = await CreateOrgAsync(organizationRepository);   // locked: migration + scheduled
        var orgChurn = await CreateOrgAsync(organizationRepository);       // locked: churn + discount applied
        var orgLockedNoop = await CreateOrgAsync(organizationRepository);  // locked, but CSV row is a no-op
        var orgUnlocked = await CreateOrgAsync(organizationRepository);    // not locked: control

        // orgScheduled: scheduled migration row → locked.
        await assignmentRepository.CreateAsync(new OrganizationPlanMigrationCohortAssignment
        {
            OrganizationId = orgScheduled.Id,
            CohortId = migrationCohort.Id,
            ScheduledDate = DateTime.UtcNow,
        });
        // orgChurn: churn discount applied → locked.
        await assignmentRepository.CreateAsync(new OrganizationPlanMigrationCohortAssignment
        {
            OrganizationId = orgChurn.Id,
            CohortId = churnCohort.Id,
            ChurnDiscountAppliedDate = DateTime.UtcNow,
        });
        // orgLockedNoop: locked (scheduled migration) and the CSV keeps it on the same cohort.
        await assignmentRepository.CreateAsync(new OrganizationPlanMigrationCohortAssignment
        {
            OrganizationId = orgLockedNoop.Id,
            CohortId = migrationCohort.Id,
            ScheduledDate = DateTime.UtcNow,
        });
        // orgUnlocked: no lifecycle dates → free to move.
        await assignmentRepository.CreateAsync(new OrganizationPlanMigrationCohortAssignment
        {
            OrganizationId = orgUnlocked.Id,
            CohortId = migrationCohort.Id,
        });

        var rows = new[]
        {
            new ResolvedCohortBulkAssignmentRow(orgScheduled.Id, destinationCohort.Id), // reassign → SKIP
            new ResolvedCohortBulkAssignmentRow(orgChurn.Id, null),                     // unassign → SKIP
            new ResolvedCohortBulkAssignmentRow(orgLockedNoop.Id, migrationCohort.Id),  // no-op → NOT counted
            new ResolvedCohortBulkAssignmentRow(orgUnlocked.Id, destinationCohort.Id),  // reassign → UPDATE
        };

        var result = await assignmentRepository.SyncManyAsync(rows);

        // Only the two locked rows that would have changed are counted; the no-op is not.
        Assert.Equal(2, result.Skipped);
        Assert.Equal(0, result.Inserted);
        Assert.Equal(1, result.Updated);
        Assert.Equal(0, result.Unassigned);

        // Locked orgs are untouched.
        var scheduled = await assignmentRepository.GetByOrganizationIdAsync(orgScheduled.Id);
        Assert.NotNull(scheduled);
        Assert.Equal(migrationCohort.Id, scheduled!.CohortId);

        var churn = await assignmentRepository.GetByOrganizationIdAsync(orgChurn.Id);
        Assert.NotNull(churn);
        Assert.Equal(churnCohort.Id, churn!.CohortId);

        var noop = await assignmentRepository.GetByOrganizationIdAsync(orgLockedNoop.Id);
        Assert.NotNull(noop);
        Assert.Equal(migrationCohort.Id, noop!.CohortId);

        // The unlocked org moved as normal.
        var unlocked = await assignmentRepository.GetByOrganizationIdAsync(orgUnlocked.Id);
        Assert.NotNull(unlocked);
        Assert.Equal(destinationCohort.Id, unlocked!.CohortId);
    }

    private static async Task<Organization> CreateOrgAsync(IOrganizationRepository repo) =>
        await repo.CreateAsync(new Organization
        {
            Name = "Test Org",
            BillingEmail = "billing@example.com",
            Plan = "Enterprise (Annually) 2020",
            PlanType = PlanType.EnterpriseAnnually2020,
        });
}
