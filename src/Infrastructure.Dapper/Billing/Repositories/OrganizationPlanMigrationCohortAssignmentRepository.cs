using System.Data;
using Bit.Core.Billing.Organizations.PlanMigration.Entities;
using Bit.Core.Billing.Organizations.PlanMigration.Repositories;
using Bit.Core.Settings;
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
}
