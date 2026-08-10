#nullable enable

using System.Data;
using System.Text.Json;
using Bit.Core.Entities;
using Bit.Core.Jobs.DataMigrations;
using Bit.Core.Settings;
using Bit.Core.Utilities;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Repositories;

public class DataMigrationStateRepository : BaseRepository, IDataMigrationStateRepository
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public DataMigrationStateRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public DataMigrationStateRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<bool> ExistsAsync(string name, CancellationToken token)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "[dbo].[DataMigrationState_ReadCountByName]",
            new { Name = name, IncompleteOnly = false },
            commandType: CommandType.StoredProcedure,
            cancellationToken: token));
        return count > 0;
    }

    public async Task InitializeAsync(string name, IReadOnlyList<PartitionRange> partitions,
        CancellationToken token)
    {
        var rows = partitions.Select(p => new
        {
            Id = CoreHelpers.GenerateComb(),
            p.Partition,
            p.RangeStart,
            p.RangeEnd,
            p.TotalRows,
        });

        await using var connection = new SqlConnection(ConnectionString);
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "[dbo].[DataMigrationState_Initialize]",
                new { Name = name, Partitions = JsonSerializer.Serialize(rows, _jsonOptions) },
                commandType: CommandType.StoredProcedure,
                cancellationToken: token));
        }
        catch (SqlException e) when (e.Number is 2601 or 2627)
        {
            // Unique (Name, Partition) violation: another instance won the initialization race.
            // Its boundary set stands; ours rolled back whole.
        }
    }

    public async Task<PartitionClaim?> TryClaimAsync(string name, string owner, TimeSpan leaseDuration,
        CancellationToken token)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var row = await connection.QuerySingleOrDefaultAsync<DataMigrationState>(new CommandDefinition(
            "[dbo].[DataMigrationState_TryClaim]",
            new
            {
                Name = name,
                LeaseOwner = owner,
                LeaseExpiresDate = DateTime.UtcNow.Add(leaseDuration),
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: token));
        return row == null
            ? null
            : new PartitionClaim(row.Partition, row.RangeStart, row.RangeEnd, row.Cursor,
                row.TotalRows, row.RowsScanned, row.RowsConverted, row.RowsSkippedByRace,
                row.RowsFailed, row.StartedDate);
    }

    public async Task<bool> CheckpointAsync(string name, int partition, string owner,
        MigrationCheckpoint checkpoint, TimeSpan leaseDuration, CancellationToken token)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var affected = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "[dbo].[DataMigrationState_Checkpoint]",
            new
            {
                Name = name,
                Partition = partition,
                LeaseOwner = owner,
                LeaseExpiresDate = DateTime.UtcNow.Add(leaseDuration),
                checkpoint.Cursor,
                checkpoint.RowsScanned,
                checkpoint.RowsConverted,
                checkpoint.RowsSkippedByRace,
                checkpoint.RowsFailed,
                checkpoint.StartedDate,
                checkpoint.CompletedDate,
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: token));
        return affected == 1;
    }

    public async Task ReleaseAsync(string name, int partition, string owner, CancellationToken token)
    {
        await using var connection = new SqlConnection(ConnectionString);
        await connection.ExecuteAsync(new CommandDefinition(
            "[dbo].[DataMigrationState_Release]",
            new { Name = name, Partition = partition, LeaseOwner = owner },
            commandType: CommandType.StoredProcedure,
            cancellationToken: token));
    }

    public async Task<IReadOnlyList<PartitionProgress>> ReadProgressAsync(string name,
        CancellationToken token)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var rows = await connection.QueryAsync<PartitionProgress>(new CommandDefinition(
            "[dbo].[DataMigrationState_ReadManyByName]",
            new { Name = name },
            commandType: CommandType.StoredProcedure,
            cancellationToken: token));
        return rows.ToList();
    }

    public async Task<int> ReadIncompleteCountAsync(string name, CancellationToken token)
    {
        await using var connection = new SqlConnection(ConnectionString);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "[dbo].[DataMigrationState_ReadCountByName]",
            new { Name = name, IncompleteOnly = true },
            commandType: CommandType.StoredProcedure,
            cancellationToken: token));
    }
}
