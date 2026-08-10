#nullable enable

using System.Data;
using System.Text.Json;
using Bit.Core;
using Bit.Core.Repositories;
using Bit.Core.Settings;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Bit.Infrastructure.Dapper.Repositories;

public class OrganizationApiKeyMigrationRepository : BaseRepository, IOrganizationApiKeyMigrationRepository
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public OrganizationApiKeyMigrationRepository(GlobalSettings globalSettings)
        : this(globalSettings.SqlServer.ConnectionString, globalSettings.SqlServer.ReadOnlyConnectionString)
    { }

    public OrganizationApiKeyMigrationRepository(string connectionString, string readOnlyConnectionString)
        : base(connectionString, readOnlyConnectionString)
    { }

    public async Task<long> CountAsync(CancellationToken token)
    {
        await using var connection = new SqlConnection(ConnectionString);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "[dbo].[OrganizationApiKey_ReadCount]",
            commandType: CommandType.StoredProcedure,
            cancellationToken: token));
    }

    public async Task<OrganizationApiKeyMigrationReadResult> ReadBatchAsync(Guid cursor,
        int scanWindow, int batchSize, CancellationToken token)
    {
        await using var connection = new SqlConnection(ConnectionString);
        var rows = (await connection.QueryAsync<MigrationReadRow>(new CommandDefinition(
            "[dbo].[OrganizationApiKey_ReadManyUnprotectedAfterId]",
            new
            {
                Cursor = cursor,
                ScanWindow = scanWindow,
                BatchSize = batchSize,
                ProtectedPrefix = Constants.DatabaseFieldProtectedPrefix,
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: token))).ToList();

        var metadata = rows.Single(r => r.IsWindowMetadata);
        var candidates = rows
            .Where(r => !r.IsWindowMetadata)
            .Select(r => new OrganizationApiKeyMigrationRow(r.Id!.Value, r.ApiKey!))
            .ToList();
        return new OrganizationApiKeyMigrationReadResult(
            candidates, metadata.Id, metadata.ScannedCount ?? 0, metadata.CandidateCount ?? 0);
    }

    public async Task<int> ProtectBatchAsync(IReadOnlyList<OrganizationApiKeyMigrationUpdate> updates,
        CancellationToken token)
    {
        // Single set-based statement: atomic without an explicit transaction, one lock-acquisition
        // pass, and the per-row OriginalApiKey predicate carries the compare-and-swap semantics.
        await using var connection = new SqlConnection(ConnectionString);
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "[dbo].[OrganizationApiKey_UpdateManyApiKeys]",
            new { Updates = JsonSerializer.Serialize(updates, _jsonOptions) },
            commandType: CommandType.StoredProcedure,
            cancellationToken: token));
    }

    private sealed class MigrationReadRow
    {
        public Guid? Id { get; set; }
        public string? ApiKey { get; set; }
        public bool IsWindowMetadata { get; set; }
        public int? ScannedCount { get; set; }
        public int? CandidateCount { get; set; }
    }
}
