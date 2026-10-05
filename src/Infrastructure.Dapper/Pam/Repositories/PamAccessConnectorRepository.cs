using System.Data;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Bit.Pam.Entities;
using Bit.Pam.Models;
using Bit.Pam.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

#nullable enable

namespace Bit.Infrastructure.Dapper.Pam.Repositories;

public class PamAccessConnectorRepository : Repository<PamAccessConnector, Guid>, IPamAccessConnectorRepository
{
    public PamAccessConnectorRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public PamAccessConnectorRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    /// <summary>
    /// PamAccessConnector_Update is narrow (Name/Status/RevisionDate only — ApiKeyId, OrganizationId, CreationDate
    /// never change post-registration, and LastHeartbeatAt has its own conditional-bump sproc), so the generic
    /// whole-entity <see cref="Repository{T, TId}.ReplaceAsync"/> would pass parameters the sproc does not declare.
    /// </summary>
    public override async Task ReplaceAsync(PamAccessConnector obj)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            $"[{Schema}].[PamAccessConnector_Update]",
            new
            {
                obj.Id,
                obj.Name,
                Status = (byte)obj.Status,
                obj.RevisionDate,
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<ICollection<PamAccessConnector>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<PamAccessConnector>(
            $"[{Schema}].[PamAccessConnector_ReadByOrganizationId]",
            new { OrganizationId = organizationId },
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }

    public async Task<PamAccessConnectorDetails?> GetDetailsByApiKeyIdAsync(Guid apiKeyId)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<PamAccessConnectorDetails>(
            $"[{Schema}].[PamAccessConnectorDetails_ReadByApiKeyId]",
            new { ApiKeyId = apiKeyId },
            commandType: CommandType.StoredProcedure);

        return results.SingleOrDefault();
    }

    public async Task UpdateHeartbeatAsync(Guid accessConnectorId, DateTime now, TimeSpan minInterval)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            $"[{Schema}].[PamAccessConnector_UpdateHeartbeat]",
            new
            {
                Id = accessConnectorId,
                Now = now,
                MinIntervalSeconds = (int)minInterval.TotalSeconds,
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task CreateAssignmentAsync(PamAccessConnectorTargetAssignment assignment)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            $"[{Schema}].[PamAccessConnectorTargetAssignment_Create]",
            assignment,
            commandType: CommandType.StoredProcedure);
    }

    public async Task DeleteAssignmentAsync(Guid accessConnectorId, Guid targetSystemId)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            $"[{Schema}].[PamAccessConnectorTargetAssignment_DeleteByAccessConnectorIdTargetSystemId]",
            new { AccessConnectorId = accessConnectorId, TargetSystemId = targetSystemId },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<ICollection<PamAccessConnectorTargetAssignment>> GetAssignmentsByOrganizationIdAsync(
        Guid organizationId)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<PamAccessConnectorTargetAssignment>(
            $"[{Schema}].[PamAccessConnectorTargetAssignment_ReadByOrganizationId]",
            new { OrganizationId = organizationId },
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }

    public async Task<bool> AssignmentExistsAsync(Guid accessConnectorId, Guid targetSystemId)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var result = await connection.ExecuteScalarAsync<int?>(
            $"[{Schema}].[PamAccessConnectorTargetAssignment_ExistsByAccessConnectorIdTargetSystemId]",
            new { AccessConnectorId = accessConnectorId, TargetSystemId = targetSystemId },
            commandType: CommandType.StoredProcedure);

        return result.HasValue;
    }
}
