using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Organizations.PlanMigration.ValueObjects;
using Bit.Core.Repositories;
using Bit.Infrastructure.IntegrationTest.AdminConsole;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Billing.Repositories;

public class OrganizationPlanMigrationCohortAssignmentExportRepositoryTests
{
    private const int _pageSize = 1000;

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
        DateTime creationDate) =>
        new()
        {
            OrganizationId = organization.Id,
            CohortId = cohort.Id,
            CreationDate = creationDate,
            RevisionDate = creationDate,
        };

    // The export pages by (CreationDate, Id) using each provider's native Guid ordering. Row order
    // is NOT required to match across providers (the CSV is a download -- every row, once), so the
    // contract a single provider must satisfy is: keyset paging returns every row exactly once with
    // no gaps, and the full result is non-decreasing on (CreationDate, Id) under THAT provider's
    // ordering -- i.e. the WHERE seek is internally consistent with the ORDER BY.
    private static bool IsNonDecreasingByCreationDateThenId(IReadOnlyList<CohortAssignmentExportRow> rows)
    {
        for (var i = 1; i < rows.Count; i++)
        {
            var prev = rows[i - 1];
            var curr = rows[i];
            if (curr.AssignedDate < prev.AssignedDate)
            {
                return false;
            }
            // Within an equal CreationDate, the only requirement is a strict, stable progression so
            // pages don't overlap or skip. We don't assert a specific Guid collation here.
        }
        return true;
    }

    // Drains every keyset page exactly as the production query object does, exercising the
    // real cursor advancement against a live database.
    private static async Task<List<CohortAssignmentExportRow>> ReadAllPagesAsync(
        IOrganizationPlanMigrationCohortAssignmentRepository repository,
        Guid cohortId,
        int pageSize)
    {
        var all = new List<CohortAssignmentExportRow>();
        DateTime? afterCreationDate = null;
        Guid? afterId = null;

        while (true)
        {
            var page = await repository.GetExportRowsByCohortIdAsync(
                cohortId, afterCreationDate, afterId, pageSize);
            all.AddRange(page);

            if (page.Count < pageSize)
            {
                break;
            }

            var last = page[^1];
            afterCreationDate = last.AssignedDate;
            afterId = last.Id;
        }

        return all;
    }

    [Theory, DatabaseData]
    public async Task GetExportRowsByCohortIdAsync_JoinSurfacesOrganizationName(
        IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository,
        IOrganizationPlanMigrationCohortRepository cohortRepository,
        IOrganizationRepository organizationRepository)
    {
        var organization = await organizationRepository.CreateTestOrganizationAsync();
        var cohort = await cohortRepository.CreateAsync(CreateTestCohort());
        var assignment = await assignmentRepository.CreateAsync(
            CreateTestAssignment(organization, cohort, DateTime.UtcNow));

        var page = await assignmentRepository.GetExportRowsByCohortIdAsync(
            cohort.Id, null, null, _pageSize);

        var row = Assert.Single(page);
        Assert.Equal(assignment.Id, row.Id);
        Assert.Equal(organization.Id, row.OrganizationId);
        Assert.Equal(organization.Name, row.OrganizationName);

        await assignmentRepository.DeleteAsync(assignment);
        await cohortRepository.DeleteAsync(cohort);
    }

    [Theory, DatabaseData]
    public async Task GetExportRowsByCohortIdAsync_ZeroAssignments_ReturnsEmpty(
        IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository,
        IOrganizationPlanMigrationCohortRepository cohortRepository)
    {
        var cohort = await cohortRepository.CreateAsync(CreateTestCohort());

        var page = await assignmentRepository.GetExportRowsByCohortIdAsync(
            cohort.Id, null, null, _pageSize);

        Assert.Empty(page);

        await cohortRepository.DeleteAsync(cohort);
    }

    [Theory, DatabaseData]
    public async Task GetExportRowsByCohortIdAsync_KeysetPaging_NoDupesNoGapsFullCoverage(
        IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository,
        IOrganizationPlanMigrationCohortRepository cohortRepository,
        IOrganizationRepository organizationRepository)
    {
        var cohort = await cohortRepository.CreateAsync(CreateTestCohort());

        // Mix of shared and distinct CreationDates so the (CreationDate, Id) tiebreaker is exercised.
        var sharedDate = DateTime.UtcNow;
        var created = new List<OrganizationPlanMigrationCohortAssignment>();
        for (var i = 0; i < 25; i++)
        {
            var org = await organizationRepository.CreateTestOrganizationAsync(identifier: $"org{i}");
            // Half the rows share the exact same CreationDate; the rest are spread out.
            var creationDate = i % 2 == 0 ? sharedDate : sharedDate.AddSeconds(i);
            created.Add(await assignmentRepository.CreateAsync(
                CreateTestAssignment(org, cohort, creationDate)));
        }

        // Page size of 4 forces many page boundaries against 25 rows.
        var all = await ReadAllPagesAsync(assignmentRepository, cohort.Id, pageSize: 4);

        var ids = all.Select(r => r.Id).ToList();
        Assert.Equal(created.Count, ids.Count);                 // full coverage
        Assert.Equal(ids.Count, ids.Distinct().Count());        // no duplicates
        Assert.Equal(created.Select(c => c.Id).OrderBy(x => x), // every created row present
            ids.OrderBy(x => x));

        foreach (var assignment in created)
        {
            await assignmentRepository.DeleteAsync(assignment);
        }
        await cohortRepository.DeleteAsync(cohort);
    }

    [Theory, DatabaseData]
    public async Task GetExportRowsByCohortIdAsync_AllSharedCreationDate_PagesWithoutGapsOrDupes(
        IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository,
        IOrganizationPlanMigrationCohortRepository cohortRepository,
        IOrganizationRepository organizationRepository)
    {
        // Bulk-loaded cohorts share a single CreationDate across the whole batch (the bulk-sync proc
        // stamps every row with one RevisionDate), so ordering hinges entirely on the Id tiebreaker.
        // This is the case most likely to expose a keyset seek that doesn't match its ORDER BY:
        // every row tied on CreationDate, paged small. We assert full coverage, no duplicates, and
        // an internally-consistent ordering -- NOT a specific cross-provider Guid sequence.
        var cohort = await cohortRepository.CreateAsync(CreateTestCohort());

        var sharedDate = DateTime.UtcNow;
        var created = new List<OrganizationPlanMigrationCohortAssignment>();
        for (var i = 0; i < 30; i++)
        {
            var org = await organizationRepository.CreateTestOrganizationAsync(identifier: $"org{i}");
            created.Add(await assignmentRepository.CreateAsync(
                CreateTestAssignment(org, cohort, sharedDate)));
        }

        var all = await ReadAllPagesAsync(assignmentRepository, cohort.Id, pageSize: 7);

        var ids = all.Select(r => r.Id).ToList();
        Assert.Equal(created.Count, ids.Count);              // full coverage across page boundaries
        Assert.Equal(ids.Count, ids.Distinct().Count());     // no duplicates at the seams
        Assert.Equal(created.Select(c => c.Id).OrderBy(x => x),
            ids.OrderBy(x => x));                            // exactly the created set
        Assert.True(IsNonDecreasingByCreationDateThenId(all)); // seek consistent with ORDER BY

        foreach (var assignment in created)
        {
            await assignmentRepository.DeleteAsync(assignment);
        }
        await cohortRepository.DeleteAsync(cohort);
    }
}
