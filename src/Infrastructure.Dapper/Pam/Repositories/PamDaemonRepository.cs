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

public class PamDaemonRepository : Repository<PamDaemon, Guid>, IPamDaemonRepository
{
    public PamDaemonRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public PamDaemonRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    /// <summary>
    /// PamDaemon_Update is narrow (Name/Status/RevisionDate only — ApiKeyId, OrganizationId, CreationDate never
    /// change post-registration, and LastHeartbeatAt has its own conditional-bump sproc), so the generic
    /// whole-entity <see cref="Repository{T, TId}.ReplaceAsync"/> would pass parameters the sproc does not declare.
    /// </summary>
    public override async Task ReplaceAsync(PamDaemon obj)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            $"[{Schema}].[PamDaemon_Update]",
            new
            {
                obj.Id,
                obj.Name,
                Status = (byte)obj.Status,
                obj.RevisionDate,
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<ICollection<PamDaemon>> GetManyByOrganizationIdAsync(Guid organizationId)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<PamDaemon>(
            $"[{Schema}].[PamDaemon_ReadByOrganizationId]",
            new { OrganizationId = organizationId },
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }

    public async Task<PamDaemonDetails?> GetDetailsByApiKeyIdAsync(Guid apiKeyId)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<PamDaemonDetails>(
            $"[{Schema}].[PamDaemonDetails_ReadByApiKeyId]",
            new { ApiKeyId = apiKeyId },
            commandType: CommandType.StoredProcedure);

        return results.SingleOrDefault();
    }

    public async Task UpdateHeartbeatAsync(Guid daemonId, DateTime now, TimeSpan minInterval)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            $"[{Schema}].[PamDaemon_UpdateHeartbeat]",
            new
            {
                Id = daemonId,
                Now = now,
                MinIntervalSeconds = (int)minInterval.TotalSeconds,
            },
            commandType: CommandType.StoredProcedure);
    }

    public async Task CreateAssignmentAsync(PamDaemonTargetAssignment assignment)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            $"[{Schema}].[PamDaemonTargetAssignment_Create]",
            assignment,
            commandType: CommandType.StoredProcedure);
    }

    public async Task DeleteAssignmentAsync(Guid daemonId, Guid targetSystemId)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            $"[{Schema}].[PamDaemonTargetAssignment_DeleteByDaemonIdTargetSystemId]",
            new { DaemonId = daemonId, TargetSystemId = targetSystemId },
            commandType: CommandType.StoredProcedure);
    }

    public async Task<ICollection<PamDaemonTargetAssignment>> GetAssignmentsByOrganizationIdAsync(Guid organizationId)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<PamDaemonTargetAssignment>(
            $"[{Schema}].[PamDaemonTargetAssignment_ReadByOrganizationId]",
            new { OrganizationId = organizationId },
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }

    public async Task<bool> AssignmentExistsAsync(Guid daemonId, Guid targetSystemId)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var result = await connection.ExecuteScalarAsync<int?>(
            $"[{Schema}].[PamDaemonTargetAssignment_ExistsByDaemonIdTargetSystemId]",
            new { DaemonId = daemonId, TargetSystemId = targetSystemId },
            commandType: CommandType.StoredProcedure);

        return result.HasValue;
    }
}
