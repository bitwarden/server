using System.Data;
using System.Text.Json;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Billing.Repositories;

public class OrganizationPlanMigrationCohortAssignmentRepository(
    GlobalSettings globalSettings)
    : Repository<OrganizationPlanMigrationCohortAssignment, Guid>(
        globalSettings.SqlServer.ConnectionString,
        globalSettings.SqlServer.ReadOnlyConnectionString),
        IOrganizationPlanMigrationCohortAssignmentRepository
{
    public async Task<OrganizationPlanMigrationCohortAssignment?> GetByOrganizationIdAsync(Guid organizationId)
    {
        await using var connection = new SqlConnection(ConnectionString);

        var results = await connection.QueryAsync<OrganizationPlanMigrationCohortAssignment>(
            $"[{Schema}].[{Table}_ReadByOrganizationId]",
            new { OrganizationId = organizationId },
            commandType: CommandType.StoredProcedure);

        return results.SingleOrDefault();
    }

    public async Task<int> GetCohortNonPendingAssignmentsCountAsync(Guid cohortId)
    {
        await using var connection = new SqlConnection(ConnectionString);

        return await connection.ExecuteScalarAsync<int>(
            $"[{Schema}].[{Table}_ReadNonPendingCountByCohortId]",
            new { CohortId = cohortId },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<IReadOnlyList<CohortAssignmentExportRow>> GetExportRowsByCohortIdAsync(
        Guid cohortId, DateTime? afterCreationDate, Guid? afterId, int take)
    {
        if (afterCreationDate is null != (afterId is null))
        {
            throw new ArgumentException("afterCreationDate and afterId must both be set or both be null.");
        }

        await using var connection = new SqlConnection(ReadOnlyConnectionString);

        var results = await connection.QueryAsync<CohortAssignmentExportRow>(
            $"[{Schema}].[{Table}_ReadManyExportByCohortId]",
            new
            {
                CohortId = cohortId,
                AfterCreationDate = afterCreationDate,
                AfterId = afterId,
                Take = take,
            },
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }

    public async Task<CohortBulkAssignmentSummary> SyncManyAsync(
        IEnumerable<ResolvedCohortBulkAssignmentRow> rows)
    {
        var payload = rows.Select(row => new
        {
            Id = row.CohortId.HasValue ? CoreHelpers.GenerateComb() : (Guid?)null,
            row.OrganizationId,
            row.CohortId,
        });
        var jsonData = JsonSerializer.Serialize(payload);

        await using var connection = new SqlConnection(ConnectionString);

        return await connection.QuerySingleAsync<CohortBulkAssignmentSummary>(
            $"[{Schema}].[{Table}_UpdateManySync]",
            new { JsonData = jsonData, RevisionDate = DateTime.UtcNow },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<IReadOnlyList<OrganizationPlanMigrationCohortAssignment>> GetSendInvoiceCandidatesInWindowAsync(int minDays, int maxDays)
    {
        await using var connection = new SqlConnection(ReadOnlyConnectionString);

        var results = await connection.QueryAsync<OrganizationPlanMigrationCohortAssignment>(
            $"[{Schema}].[{Table}_ReadManyByExpirationDateRange]",
            new { MinDays = minDays, MaxDays = maxDays },
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }
}
