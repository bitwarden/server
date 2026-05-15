using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Enums;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Organizations.PlanMigration.ValueObjects;
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
            CreatedAt = now,
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
    public async Task ReplaceAsync_UpdatesMutableColumns_AndIgnoresCreatedAt(
        IOrganizationPlanMigrationCohortRepository repository)
    {
        // The Update SP / EF override both ignore CreatedAt, so the baseline is whatever
        // CreatedAt the row was inserted with.
        var cohort = await repository.CreateAsync(CreateTestCohort());
        var baseline = await repository.GetByIdAsync(cohort.Id);
        Assert.NotNull(baseline);
        var baselineCreatedAt = baseline.CreatedAt;

        // Mutate every column except Id and CreatedAt, then attempt to mutate CreatedAt too.
        baseline.Name = $"renamed-{Guid.NewGuid()}";
        baseline.MigrationPathId = MigrationPaths.Enterprise2020MonthlyToCurrent.Id;
        baseline.ProactiveDiscountCouponCode = "NEW-PROACTIVE";
        baseline.ChurnDiscountCouponCode = "NEW-CHURN";
        baseline.IsActive = true;
        baseline.RevisionDate = DateTime.UtcNow;
        baseline.CreatedAt = DateTime.UtcNow.AddYears(-10); // Should be ignored on every provider

        await repository.ReplaceAsync(baseline);

        var result = await repository.GetByIdAsync(cohort.Id);
        Assert.NotNull(result);
        Assert.Equal(baseline.Name, result.Name);
        Assert.Equal(MigrationPaths.Enterprise2020MonthlyToCurrent.Id, result.MigrationPathId);
        Assert.Equal("NEW-PROACTIVE", result.ProactiveDiscountCouponCode);
        Assert.Equal("NEW-CHURN", result.ChurnDiscountCouponCode);
        Assert.True(result.IsActive);
        // CreatedAt should not have moved -- the Update SP and EF override both drop the write.
        Assert.Equal(baselineCreatedAt, result.CreatedAt);

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
}
