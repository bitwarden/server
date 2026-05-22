using System.Data;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
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
}
