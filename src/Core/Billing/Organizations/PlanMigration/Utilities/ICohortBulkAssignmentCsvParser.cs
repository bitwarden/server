using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Microsoft.AspNetCore.Http;

namespace Bit.Core.Billing.Organizations.PlanMigration.Utilities;

public interface ICohortBulkAssignmentCsvParser
{
    CohortBulkAssignmentParseResult Parse(IFormFile file);
}

public record CohortBulkAssignmentParseResult(
    IReadOnlyList<RawCohortBulkAssignmentRow> Rows,
    IReadOnlyList<CohortBulkAssignmentError> Errors);
