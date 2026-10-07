using Bit.DataMigrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Bit.Infrastructure.IntegrationTest.DataMigrations;

public record ScratchRow(Guid Id, string? Value);

/// <summary>
/// A throwaway table for one test, with a <c>Value</c> column that rejects <c>'boom'</c> so writes can be made to fail.
/// Dropped, along with its migration's state, on dispose.
/// </summary>
public sealed class ScratchTable(IServiceProvider services) : IAsyncDisposable
{
    public string Name { get; } = $"DmTest_{Guid.NewGuid():N}";

    public static async Task<ScratchTable> CreateAsync(IServiceProvider services, IEnumerable<string?> values)
    {
        var table = new ScratchTable(services);
        await table.ExecuteAsync((db, q) =>
        {
            var idType = db.Database.ProviderName switch
            {
                "Microsoft.EntityFrameworkCore.SqlServer" => "UNIQUEIDENTIFIER",
                "Npgsql.EntityFrameworkCore.PostgreSQL" => "UUID",
                "Pomelo.EntityFrameworkCore.MySql" => "CHAR(36)",
                _ => "TEXT",
            };
            return ($"CREATE TABLE {q(table.Name)} ({q("Id")} {idType} NOT NULL PRIMARY KEY, " +
                    $"{q("Value")} VARCHAR(50) NULL CHECK ({q("Value")} <> 'boom'))", []);
        });
        await table.InsertAsync(values);
        return table;
    }

    public async Task InsertAsync(IEnumerable<string?> values)
    {
        var rows = values.Select(v => new ScratchRow(Guid.NewGuid(), v)).ToList();
        if (rows.Count > 0)
        {
            await ExecuteAsync((_, q) => (
                $"INSERT INTO {q(Name)} ({q("Id")}, {q("Value")}) VALUES " +
                string.Join(", ", rows.Select((r, i) => r.Value is null ? $"({{{i}}}, NULL)" : $"({{{i}}}, '{r.Value}')")),
                rows.Select(r => (object)r.Id).ToArray()));
        }
    }

    public async Task<List<ScratchRow>> ReadAllAsync()
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        Func<string, string> q = db.GetService<ISqlGenerationHelper>().DelimitIdentifier;
        var sql = $"SELECT {q("Id")}, {q("Value")} FROM {q(Name)}";
        return await db.Database.SqlQueryRaw<ScratchRow>(sql).ToListAsync();
    }

    public Task UpdateAsync(Guid id, string value) =>
        ExecuteAsync((_, q) => ($"UPDATE {q(Name)} SET {q("Value")} = '{value}' WHERE {q("Id")} = {{0}}", [id]));

    public async ValueTask DisposeAsync()
    {
        await ExecuteAsync((_, q) => ($"DROP TABLE {q(Name)}", []));
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DbContext>().Set<DataMigrationState>()
            .Where(s => s.Name == Name).ExecuteDeleteAsync();
    }

    private async Task ExecuteAsync(Func<DbContext, Func<string, string>, (string Sql, object[] Args)> build)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        var (sql, args) = build(db, db.GetService<ISqlGenerationHelper>().DelimitIdentifier);
        await db.Database.ExecuteSqlRawAsync(sql, args);
    }
}

/// <summary>
/// Uppercases <see cref="ScratchRow.Value"/>, swapping <c>null</c> and <c>"to-null"</c> for <c>"was-null"</c> and
/// <c>null</c>; <c>"throw"</c> fails to transform, and <c>"cas-throw"</c> and <c>"too-long"</c> fail to write.
/// </summary>
public class SampleMigration(ScratchTable table, int batchSize = 3, int partitionCount = 1, int maxParallelBatches = 1)
    : DataMigration<string?>
{
    public override string Name => table.Name;
    protected override string Table => table.Name;
    protected override string Column => "Value";
    public override int BatchSize => batchSize;
    public override int PartitionCount => partitionCount;
    public override int MaxParallelBatches => maxParallelBatches;
    public Action<string?>? OnTransform { get; init; }

    protected override ValueTask<string?> TransformAsync(string? value, CancellationToken cancellationToken)
    {
        OnTransform?.Invoke(value);
        return ValueTask.FromResult(value switch
        {
            "throw" => throw new InvalidOperationException(),
            "cas-throw" => "boom",
            "too-long" => new string('X', 51),
            "to-null" => null,
            null => "was-null",
            _ => value.ToUpperInvariant(),
        });
    }
}
