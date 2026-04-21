#nullable enable

using System.Data;
using Bit.Core.Dirt.Entities;
using Bit.Core.Dirt.Repositories;
using Bit.Core.Settings;
using Bit.Infrastructure.Dapper.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Dirt.Repositories;

public class OrganizationEventCleanupRepository : BaseRepository, IOrganizationEventCleanupRepository
{
    public OrganizationEventCleanupRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public OrganizationEventCleanupRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<OrganizationEventCleanup?> GetNextPendingAsync()
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            return await connection.QuerySingleOrDefaultAsync<OrganizationEventCleanup>(
                "[dbo].[OrganizationEventCleanup_ReadNextPending]",
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task MarkStartedAsync(Guid id)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.ExecuteAsync(
                "[dbo].[OrganizationEventCleanup_MarkStarted]",
                new { Id = id },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task IncrementProgressAsync(Guid id, long delta)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.ExecuteAsync(
                "[dbo].[OrganizationEventCleanup_IncrementProgress]",
                new { Id = id, Delta = delta },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task MarkCompletedAsync(Guid id)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.ExecuteAsync(
                "[dbo].[OrganizationEventCleanup_MarkCompleted]",
                new { Id = id },
                commandType: CommandType.StoredProcedure);
        }
    }

    public async Task RecordErrorAsync(Guid id, string message)
    {
        using (var connection = new SqlConnection(ConnectionString))
        {
            await connection.ExecuteAsync(
                "[dbo].[OrganizationEventCleanup_RecordError]",
                new { Id = id, Message = message },
                commandType: CommandType.StoredProcedure);
        }
    }
}
