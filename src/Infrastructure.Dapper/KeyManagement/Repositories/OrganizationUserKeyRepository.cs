using System.Data;
using System.Text.Json;
using Bit.Core.KeyManagement.Models.Data;
using Bit.Core.KeyManagement.Repositories;
using Bit.Core.Settings;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.KeyManagement.Repositories;

public class OrganizationUserKeyRepository : IOrganizationUserKeyRepository
{
    private readonly string _connectionString;

    public OrganizationUserKeyRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString)
    {
    }

    public OrganizationUserKeyRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<ICollection<OrganizationUserV2UpgradeDetails>> GetManyPendingV2UpgradesByOrganizationIdAsync(
        Guid organizationId)
    {
        await using var connection = new SqlConnection(_connectionString);

        var results = await connection.QueryAsync<OrganizationUserV2UpgradeDetails>(
            "[dbo].[OrganizationUser_ReadManyV2UpgradeDetailsByOrganizationId]",
            new { OrganizationId = organizationId },
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }

    public async Task<ICollection<Guid>> UpdateManyV2UpgradedAccountRecoveryKeysAsync(Guid organizationId,
        IEnumerable<OrganizationUserAccountRecoveryKeyUpdate> updates, DateTime revisionDate)
    {
        await using var connection = new SqlConnection(_connectionString);

        var updatedIds = await connection.QueryAsync<Guid>(
            "[dbo].[OrganizationUser_UpdateManyV2UpgradedAccountRecoveryKeys]",
            new
            {
                OrganizationId = organizationId,
                OrganizationUserJson = JsonSerializer.Serialize(updates),
                RevisionDate = revisionDate
            },
            commandType: CommandType.StoredProcedure);

        return updatedIds.ToList();
    }
}
