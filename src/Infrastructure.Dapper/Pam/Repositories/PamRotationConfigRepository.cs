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

public class PamRotationConfigRepository : Repository<PamRotationConfig, Guid>, IPamRotationConfigRepository
{
    public PamRotationConfigRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public PamRotationConfigRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<PamRotationConfig?> GetByCipherIdAsync(Guid cipherId)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<PamRotationConfig>(
            $"[{Schema}].[PamRotationConfig_ReadByCipherId]",
            new { CipherId = cipherId },
            commandType: CommandType.StoredProcedure);

        return results.SingleOrDefault();
    }

    public async Task<PamRotationConfigDetails?> GetDetailsByIdAsync(Guid id)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<PamRotationConfigDetails>(
            $"[{Schema}].[PamRotationConfig_ReadDetailsById]",
            new { Id = id },
            commandType: CommandType.StoredProcedure);

        return results.SingleOrDefault();
    }

    public async Task<ICollection<PamRotationConfigDetails>> GetManyDetailsByOrganizationIdAsync(Guid organizationId)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<PamRotationConfigDetails>(
            $"[{Schema}].[PamRotationConfig_ReadManyByOrganizationId]",
            new { OrganizationId = organizationId },
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }

    public async Task<ICollection<PamRotationConfig>> GetManyDueAsync(DateTime now)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var results = await connection.QueryAsync<PamRotationConfig>(
            $"[{Schema}].[PamRotationConfig_ReadManyDue]",
            new { Now = now },
            commandType: CommandType.StoredProcedure);

        return results.ToList();
    }

    public async Task<bool> AnyByTargetSystemWithTerminateSessionsAsync(Guid targetSystemId)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var result = await connection.ExecuteScalarAsync<int?>(
            $"[{Schema}].[PamRotationConfig_AnyByTargetSystemWithTerminateSessions]",
            new { TargetSystemId = targetSystemId },
            commandType: CommandType.StoredProcedure);

        return result.HasValue;
    }

    public async Task DeleteWithJobsAsync(Guid configId)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(
            $"[{Schema}].[PamRotationConfig_DeleteWithJobs]",
            new { Id = configId },
            commandType: CommandType.StoredProcedure);
    }
}
