using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;

namespace Bit.Core.Billing.Organizations.PlanMigration.Queries;

public interface IExportCohortAssignmentsQuery
{
    /// <summary>
    /// Streams every organization assigned to the cohort as <see cref="CohortAssignmentExportRow"/>
    /// records, in a deterministic, cross-provider-stable order. Pure data: the caller is
    /// responsible for any CSV formatting or HTTP streaming. The cohort's current state is read at
    /// enumeration time (no caching).
    /// </summary>
    IAsyncEnumerable<CohortAssignmentExportRow> GetByCohortIdAsync(Guid cohortId);
}

public class ExportCohortAssignmentsQuery(
    IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository)
    : IExportCohortAssignmentsQuery
{
    // Bounded page size keeps memory flat regardless of cohort size; each page is its own
    // short-lived database read.
    private const int _pageSize = 1000;

    public async IAsyncEnumerable<CohortAssignmentExportRow> GetByCohortIdAsync(Guid cohortId)
    {
        DateTime? afterCreationDate = null;
        Guid? afterId = null;

        while (true)
        {
            var page = await assignmentRepository.GetExportRowsByCohortIdAsync(
                cohortId, afterCreationDate, afterId, _pageSize);

            foreach (var row in page)
            {
                yield return row;
            }

            // A page shorter than the requested size means there is nothing left to read.
            if (page.Count < _pageSize)
            {
                yield break;
            }

            var last = page[^1];
            afterCreationDate = last.AssignedDate;
            afterId = last.Id;
        }
    }
}
