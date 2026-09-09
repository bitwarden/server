using Bit.Core.Billing.Commands;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Billing.Organizations.PlanMigration.Utilities;
using Bit.Core.Billing.Organizations.PlanMigration.ValueObjects;
using Bit.Core.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Billing.Organizations.PlanMigration.Commands;

public class BulkSyncCohortAssignmentsCommand(
    ILogger<BulkSyncCohortAssignmentsCommand> logger,
    ICohortBulkAssignmentCsvParser parser,
    IOrganizationRepository organizationRepository,
    IOrganizationPlanMigrationCohortRepository cohortRepository,
    IOrganizationPlanMigrationCohortAssignmentRepository assignmentRepository)
    : BaseBillingCommand<BulkSyncCohortAssignmentsCommand>(logger), IBulkSyncCohortAssignmentsCommand
{
    public Task<BillingCommandResult<CohortBulkAssignmentResult>> Run(IFormFile file) =>
        HandleAsync<CohortBulkAssignmentResult>(async () =>
        {
            var parseResult = parser.Parse(file);
            var errors = new List<CohortBulkAssignmentError>(parseResult.Errors);

            // 1. Parse org ids; collect the valid (line, orgId, trimmed cohort name) tuples.
            var parsed = new List<(int Line, Guid OrgId, string CohortName)>();
            foreach (var row in parseResult.Rows)
            {
                if (!Guid.TryParse(row.OrganizationId, out var orgId))
                {
                    errors.Add(new CohortBulkAssignmentError(
                        row.LineNumber, $"'{row.OrganizationId}' is not a valid organization id."));
                    continue;
                }
                parsed.Add((row.LineNumber, orgId, row.CohortName.Trim()));
            }

            // 2. Duplicate org id within the CSV (MERGE can't take two source rows per target).
            var seen = new HashSet<Guid>();
            var duplicates = new HashSet<Guid>();
            foreach (var p in parsed)
            {
                if (!seen.Add(p.OrgId))
                {
                    duplicates.Add(p.OrgId);
                    errors.Add(new CohortBulkAssignmentError(
                        p.Line, $"Organization {p.OrgId} appears more than once in the file."));
                }
            }

            // 3. Existence + plan-type lookup (one read).
            var orgIds = parsed.Select(p => p.OrgId).Distinct().ToList();
            var planTypes = (await organizationRepository.GetPlanTypesByOrganizationIdsAsync(orgIds))
                .ToDictionary(o => o.OrganizationId, o => o.PlanType);

            // 4. Cohort-name resolution (one read of the distinct non-empty names).
            var names = parsed
                .Where(p => p.CohortName.Length > 0)
                .Select(p => p.CohortName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var cohortsByName = (names.Count == 0
                    ? []
                    : await cohortRepository.GetManyByNamesAsync(names))
                .ToDictionary(c => c.Name.Trim(), c => c, StringComparer.OrdinalIgnoreCase);

            // 5. Resolve rows + accumulate existence/resolution errors + count plan mismatches.
            var resolved = new List<ResolvedCohortBulkAssignmentRow>();
            var planMismatch = 0;
            foreach (var p in parsed)
            {
                if (duplicates.Contains(p.OrgId))
                {
                    continue;
                }

                if (!planTypes.TryGetValue(p.OrgId, out var planType))
                {
                    errors.Add(new CohortBulkAssignmentError(
                        p.Line, $"Organization {p.OrgId} does not exist."));
                    continue;
                }

                if (p.CohortName.Length == 0)
                {
                    resolved.Add(new ResolvedCohortBulkAssignmentRow(p.OrgId, null));
                    continue;
                }

                if (!cohortsByName.TryGetValue(p.CohortName, out var cohort))
                {
                    errors.Add(new CohortBulkAssignmentError(
                        p.Line, $"Cohort '{p.CohortName}' does not match any cohort."));
                    continue;
                }

                resolved.Add(new ResolvedCohortBulkAssignmentRow(p.OrgId, cohort.Id));

                if (cohort.MigrationPathId is { } pathId &&
                    CohortType.From(pathId) is CohortType.Migration migration &&
                    migration.Path.FromPlan != planType)
                {
                    planMismatch++;
                }
            }

            if (errors.Count > 0)
            {
                return new CohortBulkAssignmentResult { Errors = errors };
            }

            var summary = await assignmentRepository.SyncManyAsync(resolved);
            summary.PlanMismatch = planMismatch;
            return new CohortBulkAssignmentResult { Summary = summary };
        });
}
