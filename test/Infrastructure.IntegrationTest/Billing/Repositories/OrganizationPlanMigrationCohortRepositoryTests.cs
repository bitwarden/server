using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Organizations.PlanMigration.ValueObjects;
using Bit.Core.Repositories;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Billing.Repositories;

public class OrganizationPlanMigrationCohortRepositoryTests
{
    private static OrganizationPlanMigrationCohort CreateTestCohort(
        string? name = null,
        MigrationPathId? migrationPathId = null,
        string? proactiveCoupon = null,
        string? churnCoupon = null,
        bool isActive = false)
    {
        var now = DateTime.UtcNow;
        return new OrganizationPlanMigrationCohort
        {
            Name = name ?? $"cohort-{Guid.NewGuid()}",
            MigrationPathId = migrationPathId,
            ProactiveDiscountCouponCode = proactiveCoupon,
            ChurnDiscountCouponCode = churnCoupon,
            IsActive = isActive,
            CreationDate = now,
            RevisionDate = now,
        };
    }

    [Theory, DatabaseData]
    public async Task CreateAsync_GetByIdAsync_RoundTrip(
        IOrganizationPlanMigrationCohortRepository repository)
    {
        var cohort = await repository.CreateAsync(CreateTestCohort(
            migrationPathId: MigrationPaths.Enterprise2020AnnualToCurrent.Id,
            proactiveCoupon: "PROACTIVE10",
            churnCoupon: "CHURN15",
            isActive: true));

        var result = await repository.GetByIdAsync(cohort.Id);

        Assert.NotNull(result);
        Assert.Equal(cohort.Id, result.Id);
        Assert.Equal(cohort.Name, result.Name);
        Assert.Equal(MigrationPaths.Enterprise2020AnnualToCurrent.Id, result.MigrationPathId);
        Assert.Equal("PROACTIVE10", result.ProactiveDiscountCouponCode);
        Assert.Equal("CHURN15", result.ChurnDiscountCouponCode);
        Assert.True(result.IsActive);

        // Cleanup
        await repository.DeleteAsync(result);
    }

    [Theory, DatabaseData]
    public async Task CreateAsync_DuplicateName_Throws(
        IOrganizationPlanMigrationCohortRepository repository)
    {
        var sharedName = $"cohort-{Guid.NewGuid()}";
        var first = await repository.CreateAsync(CreateTestCohort(name: sharedName));

        await Assert.ThrowsAnyAsync<Exception>(
            () => repository.CreateAsync(CreateTestCohort(name: sharedName)));

        // Cleanup
        await repository.DeleteAsync(first);
    }

    [Theory, DatabaseData]
    public async Task ReplaceAsync_UpdatesMutableColumns_AndIgnoresCreationDate(
        IOrganizationPlanMigrationCohortRepository repository)
    {
        // The Update SP / EF override both ignore CreationDate, so the baseline is whatever
        // CreationDate the row was inserted with.
        var cohort = await repository.CreateAsync(CreateTestCohort());
        var baseline = await repository.GetByIdAsync(cohort.Id);
        Assert.NotNull(baseline);
        var baselineCreationDate = baseline.CreationDate;

        // Mutate every column except Id and CreationDate, then attempt to mutate CreationDate too.
        baseline.Name = $"renamed-{Guid.NewGuid()}";
        baseline.MigrationPathId = MigrationPaths.Enterprise2020MonthlyToCurrent.Id;
        baseline.ProactiveDiscountCouponCode = "NEW-PROACTIVE";
        baseline.ChurnDiscountCouponCode = "NEW-CHURN";
        baseline.IsActive = true;
        baseline.RevisionDate = DateTime.UtcNow;
        baseline.CreationDate = DateTime.UtcNow.AddYears(-10); // Should be ignored on every provider

        await repository.ReplaceAsync(baseline);

        var result = await repository.GetByIdAsync(cohort.Id);
        Assert.NotNull(result);
        Assert.Equal(baseline.Name, result.Name);
        Assert.Equal(MigrationPaths.Enterprise2020MonthlyToCurrent.Id, result.MigrationPathId);
        Assert.Equal("NEW-PROACTIVE", result.ProactiveDiscountCouponCode);
        Assert.Equal("NEW-CHURN", result.ChurnDiscountCouponCode);
        Assert.True(result.IsActive);
        // CreationDate should not have moved -- the Update SP and EF override both drop the write.
        Assert.Equal(baselineCreationDate, result.CreationDate);

        // Cleanup
        await repository.DeleteAsync(result);
    }

    [Theory, DatabaseData]
    public async Task DeleteAsync_RemovesCohort(
        IOrganizationPlanMigrationCohortRepository repository)
    {
        var cohort = await repository.CreateAsync(CreateTestCohort());

        await repository.DeleteAsync(cohort);

        var result = await repository.GetByIdAsync(cohort.Id);
        Assert.Null(result);
    }

    [Theory, DatabaseData]
    public async Task SearchWithCountsAsync_OneCohort_ReturnsThatCohort(
        IOrganizationPlanMigrationCohortRepository repository)
    {
        var uniqueName = $"slice1-{Guid.NewGuid()}";
        var cohort = await repository.CreateAsync(CreateTestCohort(name: uniqueName));

        var results = (await repository.SearchWithCountsAsync(uniqueName, 0, 25)).ToList();

        var row = Assert.Single(results);
        Assert.Equal(cohort.Id, row.Cohort.Id);
        Assert.Equal(uniqueName, row.Cohort.Name);

        await repository.DeleteAsync(cohort);
    }

    [Theory, DatabaseData]
    public async Task SearchWithCountsAsync_MigrationCohort_CountsPendingScheduledMigrated(
        IOrganizationPlanMigrationCohortRepository repository,
        IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository,
        IOrganizationRepository organizationRepository)
    {
        var uniqueName = $"mig-{Guid.NewGuid()}";
        var cohort = await repository.CreateAsync(CreateTestCohort(
            name: uniqueName,
            migrationPathId: MigrationPaths.Enterprise2020AnnualToCurrent.Id));

        var pendingOrg = await organizationRepository.CreateTestOrganizationAsync();
        var scheduledOrg = await organizationRepository.CreateTestOrganizationAsync();
        var migratedOrg = await organizationRepository.CreateTestOrganizationAsync();

        await assignmentRepository.CreateAsync(new OrganizationPlanMigrationCohortAssignment
        {
            OrganizationId = pendingOrg.Id,
            CohortId = cohort.Id,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
        });
        await assignmentRepository.CreateAsync(new OrganizationPlanMigrationCohortAssignment
        {
            OrganizationId = scheduledOrg.Id,
            CohortId = cohort.Id,
            ScheduledDate = DateTime.UtcNow,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
        });
        await assignmentRepository.CreateAsync(new OrganizationPlanMigrationCohortAssignment
        {
            OrganizationId = migratedOrg.Id,
            CohortId = cohort.Id,
            ScheduledDate = DateTime.UtcNow,
            MigratedDate = DateTime.UtcNow,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
        });

        var row = (await repository.SearchWithCountsAsync(uniqueName, 0, 25)).Single();

        Assert.Equal(1, row.Pending);
        Assert.Equal(1, row.Scheduled);
        Assert.Equal(1, row.Migrated);

        await repository.DeleteAsync(cohort);
    }

    [Theory, DatabaseData]
    public async Task SearchWithCountsAsync_ChurnOnlyCohort_CountsRedemptionsAsMigrated(
        IOrganizationPlanMigrationCohortRepository repository,
        IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository,
        IOrganizationRepository organizationRepository)
    {
        var uniqueName = $"churn-{Guid.NewGuid()}";
        var cohort = await repository.CreateAsync(CreateTestCohort(
            name: uniqueName,
            migrationPathId: null,
            churnCoupon: "SAVE15"));

        var pendingOrg = await organizationRepository.CreateTestOrganizationAsync();
        var redeemedOrg = await organizationRepository.CreateTestOrganizationAsync();

        await assignmentRepository.CreateAsync(new OrganizationPlanMigrationCohortAssignment
        {
            OrganizationId = pendingOrg.Id,
            CohortId = cohort.Id,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
        });
        await assignmentRepository.CreateAsync(new OrganizationPlanMigrationCohortAssignment
        {
            OrganizationId = redeemedOrg.Id,
            CohortId = cohort.Id,
            ChurnDiscountAppliedDate = DateTime.UtcNow,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
        });

        var row = (await repository.SearchWithCountsAsync(uniqueName, 0, 25)).Single();

        Assert.Equal(1, row.Pending);
        Assert.Equal(0, row.Scheduled);
        Assert.Equal(1, row.Migrated);

        await repository.DeleteAsync(cohort);
    }
}
