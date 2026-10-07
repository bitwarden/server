using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Microsoft.AspNetCore.Http;

namespace Bit.Core.Billing.Organizations.PlanMigration.Commands;

public interface IBulkSyncCohortAssignmentsCommand
{
    /// <summary>
    /// Parses the uploaded CSV and validates it (structural + semantic errors); if there are no
    /// errors, commits the assignment changes and returns the summary (with the plan-mismatch
    /// count). Any validation error ⇒ nothing is written and the errors ride in the success
    /// payload's <see cref="CohortBulkAssignmentResult.Errors"/>. Unexpected exceptions surface
    /// as the union's <c>Unhandled</c> branch.
    /// </summary>
    Task<BillingCommandResult<CohortBulkAssignmentResult>> Run(IFormFile file);
}
