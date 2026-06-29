using System.Data;
using System.Text.Json;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Models;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Billing.Repositories;

public class OrganizationPlanMigrationCohortRepository(
    GlobalSettings globalSettings)
    : Repository<OrganizationPlanMigrationCohort, Guid>(
        globalSettings.SqlServer.ConnectionString,
        globalSettings.SqlServer.ReadOnlyConnectionString),
        IOrganizationPlanMigrationCohortRepository
{
    public async Task<IReadOnlyList<OrganizationPlanMigrationCohort>> GetManyAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);

        var results = await connection.QueryAsync<OrganizationPlanMigrationCohort>(
            $"[{Schema}].[{Table}_ReadMany]",
            commandType: CommandType.StoredProcedure);

        return [.. results];
    }

    public async Task<IEnumerable<CohortListItem>> SearchWithCountsAsync(
        string? name,
        int skip,
        int take)
    {
        await using var connection = new SqlConnection(ConnectionString);

        return await connection.QueryAsync<CohortListItem, OrganizationPlanMigrationCohort, CohortListItem>(
            $"[{Schema}].[{Table}_ReadManyWithCountsByName]",
            (item, cohort) =>
            {
                item.Cohort = cohort;
                return item;
            },
            new
            {
                Name = string.IsNullOrWhiteSpace(name) ? null : name,
                Skip = skip,
                Take = take,
            },
            commandType: CommandType.StoredProcedure,
            splitOn: "Id");
    }

    public async Task<OrganizationPlanMigrationCohort?> GetByNameAsync(string name)
    {
        await using var connection = new SqlConnection(ConnectionString);

        var results = await connection.QueryAsync<OrganizationPlanMigrationCohort>(
            $"[{Schema}].[{Table}_ReadByName]",
            new { Name = name },
            commandType: CommandType.StoredProcedure);

        return results.SingleOrDefault();
    }

    public async Task<IReadOnlyList<OrganizationPlanMigrationCohort>> GetManyByNamesAsync(IEnumerable<string> names)
    {
        var payload = names
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => new { Name = name });

        await using var connection = new SqlConnection(ConnectionString);

        var results = await connection.QueryAsync<OrganizationPlanMigrationCohort>(
            $"[{Schema}].[{Table}_ReadManyByNames]",
            new { JsonData = JsonSerializer.Serialize(payload) },
            commandType: CommandType.StoredProcedure);

        return [.. results];
    }
}
