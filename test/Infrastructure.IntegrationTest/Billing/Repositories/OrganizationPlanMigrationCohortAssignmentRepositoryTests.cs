using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Organizations.PlanMigration.ValueObjects;
using Bit.Core.Repositories;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Bit.Infrastructure.IntegrationTest.Comparers;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Billing.Repositories;

public class OrganizationPlanMigrationCohortAssignmentRepositoryTests
{
    private static OrganizationPlanMigrationCohort CreateTestCohort() =>
        new()
        {
            Name = $"cohort-{Guid.NewGuid()}",
            MigrationPathId = MigrationPaths.Enterprise2020AnnualToCurrent.Id,
            IsActive = true,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
        };

    private static OrganizationPlanMigrationCohortAssignment CreateTestAssignment(
        Organization organization,
        OrganizationPlanMigrationCohort cohort,
        DateTime? scheduledAt = null,
        DateTime? migratedAt = null,
        DateTime? churnDiscountAppliedAt = null) =>
        new()
        {
            OrganizationId = organization.Id,
            CohortId = cohort.Id,
            ScheduledDate = scheduledAt,
            MigratedDate = migratedAt,
            ChurnDiscountAppliedDate = churnDiscountAppliedAt,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
        };

    [Theory, DatabaseData]
    public async Task CreateAsync_GetByIdAsync_RoundTrip(
        IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository,
        IOrganizationPlanMigrationCohortRepository cohortRepository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var cohort = await cohortRepository.CreateAsync(CreateTestCohort());
        var scheduledAt = DateTime.UtcNow;

        var assignment = await assignmentRepository.CreateAsync(
            CreateTestAssignment(organization, cohort, scheduledAt: scheduledAt));

        var result = await assignmentRepository.GetByIdAsync(assignment.Id);

        Assert.NotNull(result);
        Assert.Equal(assignment.Id, result.Id);
        Assert.Equal(organization.Id, result.OrganizationId);
        Assert.Equal(cohort.Id, result.CohortId);
        Assert.NotNull(result.ScheduledDate);
        Assert.Null(result.MigratedDate);
        Assert.Null(result.ChurnDiscountAppliedDate);

        // Cleanup (cascade from organization will also remove the assignment, but be explicit)
        await assignmentRepository.DeleteAsync(result);
        await cohortRepository.DeleteAsync(cohort);
    }

    [Theory, DatabaseData]
    public async Task CreateAsync_DuplicateOrganizationId_Throws(
        IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository,
        IOrganizationPlanMigrationCohortRepository cohortRepository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var firstCohort = await cohortRepository.CreateAsync(CreateTestCohort());
        var secondCohort = await cohortRepository.CreateAsync(CreateTestCohort());
        var first = await assignmentRepository.CreateAsync(CreateTestAssignment(organization, firstCohort));

        await Assert.ThrowsAnyAsync<Exception>(() =>
            assignmentRepository.CreateAsync(CreateTestAssignment(organization, secondCohort)));

        // Cleanup
        await assignmentRepository.DeleteAsync(first);
        await cohortRepository.DeleteAsync(firstCohort);
        await cohortRepository.DeleteAsync(secondCohort);
    }

    [Theory, DatabaseData]
    public async Task GetByOrganizationIdAsync_ReturnsAssignment(
        IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository,
        IOrganizationPlanMigrationCohortRepository cohortRepository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var cohort = await cohortRepository.CreateAsync(CreateTestCohort());
        var assignment = await assignmentRepository.CreateAsync(CreateTestAssignment(organization, cohort));

        var result = await assignmentRepository.GetByOrganizationIdAsync(organization.Id);

        Assert.NotNull(result);
        Assert.Equal(assignment.Id, result.Id);
        Assert.Equal(organization.Id, result.OrganizationId);
        Assert.Equal(cohort.Id, result.CohortId);

        // Cleanup
        await assignmentRepository.DeleteAsync(result);
        await cohortRepository.DeleteAsync(cohort);
    }

    [Theory, DatabaseData]
    public async Task GetByOrganizationIdAsync_NonExistentOrganization_ReturnsNull(
        IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository)
    {
        var result = await assignmentRepository.GetByOrganizationIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Theory, DatabaseData]
    public async Task ReplaceAsync_UpdatesMutableColumns_AndIgnoresImmutableOnes(
        IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository,
        IOrganizationPlanMigrationCohortRepository cohortRepository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var otherOrganization = await organizationRepository.CreateTestOrganizationAsync(identifier: "other");
        var cohort = await cohortRepository.CreateAsync(CreateTestCohort());
        var otherCohort = await cohortRepository.CreateAsync(CreateTestCohort());

        var assignment = await assignmentRepository.CreateAsync(CreateTestAssignment(organization, cohort));
        var baseline = await assignmentRepository.GetByIdAsync(assignment.Id);
        Assert.NotNull(baseline);
        var baselineCreationDate = baseline.CreationDate;

        // Mutate the legitimately mutable columns and ALSO attempt to mutate the immutable
        // columns. The Update SP accepts those parameters but does not assign them.
        var migratedAt = DateTime.UtcNow;
        var churnAt = DateTime.UtcNow.AddHours(-1);
        baseline.ScheduledDate = DateTime.UtcNow.AddDays(-1);
        baseline.MigratedDate = migratedAt;
        baseline.ChurnDiscountAppliedDate = churnAt;
        baseline.RevisionDate = DateTime.UtcNow;
        baseline.OrganizationId = otherOrganization.Id; // Should be ignored
        baseline.CohortId = otherCohort.Id;             // Should be ignored
        baseline.CreationDate = DateTime.UtcNow.AddYears(-10); // Should be ignored

        await assignmentRepository.ReplaceAsync(baseline);

        var result = await assignmentRepository.GetByIdAsync(assignment.Id);
        Assert.NotNull(result);
        Assert.NotNull(result.ScheduledDate);
        Assert.NotNull(result.MigratedDate);
        Assert.NotNull(result.ChurnDiscountAppliedDate);
        // Postgres timestamp and MySQL datetime(6) both store microsecond precision; .NET
        // DateTime has 100ns ticks. Round-tripping truncates the last digit, so compare with
        // LaxDateTimeComparer rather than exact equality.
        Assert.Equal(migratedAt, result.MigratedDate.Value, LaxDateTimeComparer.Default);
        Assert.Equal(churnAt, result.ChurnDiscountAppliedDate.Value, LaxDateTimeComparer.Default);
        // Immutable columns must not have moved.
        Assert.Equal(organization.Id, result.OrganizationId);
        Assert.Equal(cohort.Id, result.CohortId);
        Assert.Equal(baselineCreationDate, result.CreationDate, LaxDateTimeComparer.Default);

        // Cleanup
        await assignmentRepository.DeleteAsync(result);
        await cohortRepository.DeleteAsync(cohort);
        await cohortRepository.DeleteAsync(otherCohort);
    }

    [Theory, DatabaseData]
    public async Task DeleteAsync_RemovesAssignment(
        IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository,
        IOrganizationPlanMigrationCohortRepository cohortRepository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var cohort = await cohortRepository.CreateAsync(CreateTestCohort());
        var assignment = await assignmentRepository.CreateAsync(CreateTestAssignment(organization, cohort));

        await assignmentRepository.DeleteAsync(assignment);

        var result = await assignmentRepository.GetByIdAsync(assignment.Id);
        Assert.Null(result);

        // Cleanup
        await cohortRepository.DeleteAsync(cohort);
    }

    [Theory, DatabaseData]
    public async Task DeletingOrganization_CascadesToAssignment(
        IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository,
        IOrganizationPlanMigrationCohortRepository cohortRepository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var cohort = await cohortRepository.CreateAsync(CreateTestCohort());
        var assignment = await assignmentRepository.CreateAsync(CreateTestAssignment(organization, cohort));

        await organizationRepository.DeleteAsync(organization);

        var result = await assignmentRepository.GetByIdAsync(assignment.Id);
        Assert.Null(result);

        // Cleanup
        await cohortRepository.DeleteAsync(cohort);
    }

    [Theory, DatabaseData]
    public async Task DeletingCohort_CascadesToAssignment(
        IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository,
        IOrganizationPlanMigrationCohortRepository cohortRepository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var cohort = await cohortRepository.CreateAsync(CreateTestCohort());
        var assignment = await assignmentRepository.CreateAsync(CreateTestAssignment(organization, cohort));

        await cohortRepository.DeleteAsync(cohort);

        var result = await assignmentRepository.GetByIdAsync(assignment.Id);
        Assert.Null(result);
    }

    private static OrganizationPlanMigrationCohort CreateChurnOnlyCohort() =>
        new()
        {
            Name = $"churn-{Guid.NewGuid()}",
            MigrationPathId = null,
            ChurnDiscountCouponCode = "SAVE15",
            IsActive = true,
            CreationDate = DateTime.UtcNow,
            RevisionDate = DateTime.UtcNow,
        };

    [Theory, DatabaseData]
    public async Task GetCohortNonPendingAssignmentsCountAsync_ChurnOnly_CountsOnlyRedemptions(
        IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository,
        IOrganizationPlanMigrationCohortRepository cohortRepository,
        IOrganizationRepository organizationRepository)
    {
        var cohort = await cohortRepository.CreateAsync(CreateChurnOnlyCohort());

        var pendingOrg = await organizationRepository.CreateTestOrganizationAsync();
        var redeemedOrg = await organizationRepository.CreateTestOrganizationAsync();

        await assignmentRepository.CreateAsync(CreateTestAssignment(pendingOrg, cohort));
        await assignmentRepository.CreateAsync(CreateTestAssignment(
            redeemedOrg, cohort, churnDiscountAppliedAt: DateTime.UtcNow));

        var count = await assignmentRepository.GetCohortNonPendingAssignmentsCountAsync(cohort.Id);

        Assert.Equal(1, count);

        await cohortRepository.DeleteAsync(cohort);
    }

    [Theory, DatabaseData]
    public async Task GetCohortNonPendingAssignmentsCountAsync_MigrationCohort_CountsScheduledOrMigrated(
        IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository,
        IOrganizationPlanMigrationCohortRepository cohortRepository,
        IOrganizationRepository organizationRepository)
    {
        var cohort = await cohortRepository.CreateAsync(CreateTestCohort());

        var pendingOrg = await organizationRepository.CreateTestOrganizationAsync();
        var scheduledOrg = await organizationRepository.CreateTestOrganizationAsync();
        var migratedOrg = await organizationRepository.CreateTestOrganizationAsync();

        await assignmentRepository.CreateAsync(CreateTestAssignment(pendingOrg, cohort));
        await assignmentRepository.CreateAsync(CreateTestAssignment(scheduledOrg, cohort, scheduledAt: DateTime.UtcNow));
        await assignmentRepository.CreateAsync(CreateTestAssignment(
            migratedOrg, cohort, scheduledAt: DateTime.UtcNow, migratedAt: DateTime.UtcNow));

        var count = await assignmentRepository.GetCohortNonPendingAssignmentsCountAsync(cohort.Id);

        Assert.Equal(2, count);

        await cohortRepository.DeleteAsync(cohort);
    }
}
