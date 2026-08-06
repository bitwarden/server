using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Billing.Organizations.PlanMigration.Queries;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Billing.Organizations.PlanMigration.Queries;

[SutProviderCustomize]
public class ExportCohortAssignmentsQueryTests
{
    private const int _pageSize = 1000;

    private static CohortAssignmentExportRow Row(int sequence) =>
        new(
            Id: Guid.NewGuid(),
            OrganizationId: Guid.NewGuid(),
            OrganizationName: $"org-{sequence}",
            // Strictly increasing so the cursor advances deterministically.
            AssignedDate: new DateTime(2026, 1, 1).AddMinutes(sequence),
            ScheduledDate: null,
            MigratedDate: null);

    private static List<CohortAssignmentExportRow> Page(int count) =>
        Enumerable.Range(0, count).Select(Row).ToList();

    private static async Task<List<CohortAssignmentExportRow>> DrainAsync(
        IAsyncEnumerable<CohortAssignmentExportRow> source)
    {
        var rows = new List<CohortAssignmentExportRow>();
        await foreach (var row in source)
        {
            rows.Add(row);
        }
        return rows;
    }

    [Theory, BitAutoData]
    public async Task GetByCohortIdAsync_EmptyCohort_YieldsNothing(
        Guid cohortId,
        SutProvider<ExportCohortAssignmentsQuery> sutProvider)
    {
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .GetExportRowsByCohortIdAsync(cohortId, null, null, _pageSize)
            .Returns(new List<CohortAssignmentExportRow>());

        var result = await DrainAsync(sutProvider.Sut.GetByCohortIdAsync(cohortId));

        Assert.Empty(result);
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .Received(1)
            .GetExportRowsByCohortIdAsync(cohortId, null, null, _pageSize);
    }

    [Theory, BitAutoData]
    public async Task GetByCohortIdAsync_ShortFirstPage_StopsAfterOneRead(
        Guid cohortId,
        SutProvider<ExportCohortAssignmentsQuery> sutProvider)
    {
        var page = Page(10);
        sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .GetExportRowsByCohortIdAsync(cohortId, null, null, _pageSize)
            .Returns(page);

        var result = await DrainAsync(sutProvider.Sut.GetByCohortIdAsync(cohortId));

        Assert.Equal(page.Select(r => r.Id), result.Select(r => r.Id));
        await sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>()
            .Received(1)
            .GetExportRowsByCohortIdAsync(cohortId, Arg.Any<DateTime?>(), Arg.Any<Guid?>(), _pageSize);
    }

    [Theory, BitAutoData]
    public async Task GetByCohortIdAsync_MultiplePages_AdvancesCursorAndYieldsEveryRowOnceInOrder(
        Guid cohortId,
        SutProvider<ExportCohortAssignmentsQuery> sutProvider)
    {
        // Two full pages then a short page -> three reads, cursor advancing each time.
        var firstPage = Page(_pageSize);
        var secondPage = Page(_pageSize);
        var thirdPage = Page(5);
        var repository = sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>();

        repository.GetExportRowsByCohortIdAsync(cohortId, null, null, _pageSize)
            .Returns(firstPage);

        var firstLast = firstPage[^1];
        repository.GetExportRowsByCohortIdAsync(cohortId, firstLast.AssignedDate, firstLast.Id, _pageSize)
            .Returns(secondPage);

        var secondLast = secondPage[^1];
        repository.GetExportRowsByCohortIdAsync(cohortId, secondLast.AssignedDate, secondLast.Id, _pageSize)
            .Returns(thirdPage);

        var result = await DrainAsync(sutProvider.Sut.GetByCohortIdAsync(cohortId));

        var expected = firstPage.Concat(secondPage).Concat(thirdPage).Select(r => r.Id).ToList();
        Assert.Equal(expected, result.Select(r => r.Id));
        // Each Id appears exactly once.
        Assert.Equal(expected.Count, result.Select(r => r.Id).Distinct().Count());

        await repository.Received(3)
            .GetExportRowsByCohortIdAsync(cohortId, Arg.Any<DateTime?>(), Arg.Any<Guid?>(), _pageSize);
    }

    [Theory, BitAutoData]
    public async Task GetByCohortIdAsync_ExactlyFullPageThenEmpty_StopsAfterEmptyPage(
        Guid cohortId,
        SutProvider<ExportCohortAssignmentsQuery> sutProvider)
    {
        // A full page must trigger another read; the empty follow-up page ends the loop.
        var firstPage = Page(_pageSize);
        var repository = sutProvider.GetDependency<IOrganizationPlanMigrationCohortAssignmentRepository>();

        repository.GetExportRowsByCohortIdAsync(cohortId, null, null, _pageSize)
            .Returns(firstPage);

        var firstLast = firstPage[^1];
        repository.GetExportRowsByCohortIdAsync(cohortId, firstLast.AssignedDate, firstLast.Id, _pageSize)
            .Returns(new List<CohortAssignmentExportRow>());

        var result = await DrainAsync(sutProvider.Sut.GetByCohortIdAsync(cohortId));

        Assert.Equal(_pageSize, result.Count);
        await repository.Received(2)
            .GetExportRowsByCohortIdAsync(cohortId, Arg.Any<DateTime?>(), Arg.Any<Guid?>(), _pageSize);
    }
}
