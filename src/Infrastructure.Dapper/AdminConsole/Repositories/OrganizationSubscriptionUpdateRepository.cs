using System.Data;
using System.Text.Json;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.AdminConsole.Repositories;

public class OrganizationSubscriptionUpdateRepository(string connectionString, string readOnlyConnectionString)
    : BaseRepository(connectionString, readOnlyConnectionString),
        IOrganizationSubscriptionUpdateRepository
{
    public OrganizationSubscriptionUpdateRepository(IGlobalSettings globalSettings) : this(
        globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    {
    }

    public async Task SetToUpdateSubscriptionAsync(Guid organizationId, DateTime seatsUpdatedAt)
    {
        await using var connection = new SqlConnection(ConnectionString);

        await connection.ExecuteAsync("[dbo].[OrganizationSubscriptionUpdate_SetToUpdateSubscription]",
            new { OrganzationId = organizationId, SeatsLastUpdated = seatsUpdatedAt },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<IEnumerable<OrganizationSubscriptionUpdate>> GetUpdatesToSubscriptionAsync()
    {
        await using var connection = new SqlConnection(ConnectionString);

        return await connection
            .QueryAsync<OrganizationSubscriptionUpdate>("[dbo].[OrganizationSubscriptionUpdate_GetUpdatesToSubscription]",
                commandType: CommandType.StoredProcedure);
    }

    public async Task UpdateSubscriptionStatusAsync(IEnumerable<Guid> successfulOrganizations,
        IEnumerable<Guid> failedOrganizations)
    {
        await using var connection = new SqlConnection(ConnectionString);

        await connection.ExecuteAsync("[dbo].[OrganizationSubscriptionUpdate_UpdateSubscriptionStatus]",
            new
            {
                SuccessfulOrganizations = JsonSerializer.Serialize(successfulOrganizations),
                FailedOrganizations = JsonSerializer.Serialize(failedOrganizations)
            },
            commandType: CommandType.StoredProcedure);
    }
}
