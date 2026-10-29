using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Bit.DataMigrations;

/// <summary>
/// A data migration, registered with <see cref="DataMigrationsServiceCollectionExtensions.AddDataMigration{T}"/>.
/// Derive from <see cref="DataMigration{TValue}"/>.
/// </summary>
public abstract class DataMigration
{
    private protected DataMigration() { }

    /// <summary>A unique PascalCase name, shared by the migration's markers in every schema migration chain.</summary>
    public abstract string Name { get; }

    /// <summary>Rows read and written per batch.</summary>
    public virtual int BatchSize => 1000;

    public virtual int PartitionCount => 1;

    /// <summary>Partitions processed in parallel per run.</summary>
    public virtual int MaxParallelBatches => 1;

    public virtual TimeSpan LeaseDuration => TimeSpan.FromMinutes(10);

    internal abstract Task<List<DataMigrationState>> SplitAsync(DbContext db, CancellationToken cancellationToken);

    /// <summary>Runs the batch of rows from <paramref name="cursor"/> through <paramref name="rangeEnd"/>, inclusive.</summary>
    internal abstract Task<DataMigrationBatch> RunBatchAsync(DbContext db, string cursor, string rangeEnd, bool dryRun,
        ILogger logger, CancellationToken cancellationToken);
}

internal record DataMigrationFailure(string Key, string Error);

/// <param name="Cursor">The key of the next batch's first row, or the same cursor once <paramref name="Done"/>.</param>
internal record DataMigrationBatch(string Cursor, int Scanned, int Converted, int Skipped,
    IReadOnlyList<DataMigrationFailure> Failures, bool Done);

internal record DataMigrationRow<TValue>(Guid Id, TValue Value);

/// <summary>
/// A data migration of one <see cref="Column"/> of <see cref="Table"/>. It runs at its marker against the schema as it
/// was there, so it names the table and column as they were then, and never uses repositories or entity models. Each
/// converted value is written with check-and-set: a value changed since it was read keeps its new value and counts as
/// skipped.
/// </summary>
/// <typeparam name="TValue">The column's type, nullable if the column is.</typeparam>
public abstract class DataMigration<TValue> : DataMigration
{
    private string? _sqlServerUpdate;

    protected abstract string Table { get; }

    protected abstract string Column { get; }

    /// <summary>The table's Guid primary key column.</summary>
    protected virtual string Key => "Id";

    /// <summary>
    /// Returns the converted value, or the same value if it's already converted. Throw to count the row as failed.
    /// </summary>
    protected abstract ValueTask<TValue> TransformAsync(TValue value, CancellationToken cancellationToken);

    internal override async Task<List<DataMigrationState>> SplitAsync(DbContext db, CancellationToken cancellationToken)
    {
        var sql = Sql(db);
        var count = await db.Database.SqlQueryRaw<long>(sql.Count).SingleAsync(cancellationToken);
        if (count == 0)
        {
            return [];
        }

        var step = DataMigrationSql.Step(count, PartitionCount);
        var bounds = await db.Database.SqlQueryRaw<Guid>(sql.Bounds(step)).ToListAsync(cancellationToken);
        return DataMigrationSql.Partitions(Name, count, step, bounds.Select(b => b.ToString()).ToList());
    }

    internal override async Task<DataMigrationBatch> RunBatchAsync(DbContext db, string cursor, string rangeEnd,
        bool dryRun, ILogger logger, CancellationToken cancellationToken)
    {
        // One row past the batch, whose key is the next batch's cursor.
        var read = await db.Database.SqlQueryRaw<DataMigrationRow<TValue>>(Sql(db).Select(BatchSize + 1),
            Guid.Parse(cursor), Guid.Parse(rangeEnd)).ToListAsync(cancellationToken);
        var rows = read.Take(BatchSize).ToList();

        var changed = new List<Change>();
        var failures = new List<DataMigrationFailure>();
        foreach (var row in rows)
        {
            try
            {
                var converted = await TransformAsync(row.Value, cancellationToken);
                if (!EqualityComparer<TValue>.Default.Equals(converted, row.Value))
                {
                    changed.Add(new(row.Id, row.Value, converted));
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                failures.Add(new(row.Id.ToString(), ex.GetType().Name));
            }
        }

        var written = changed.Count;
        if (!dryRun && changed.Count > 0)
        {
            (written, var writeFailures) = await WriteAsync(db, changed, logger, cancellationToken);
            failures.AddRange(writeFailures);
        }

        var done = read.Count <= BatchSize;
        return new(done ? cursor : read[^1].Id.ToString(), rows.Count, written, rows.Count - written - failures.Count,
            failures, done);
    }

    private async Task<(int Written, List<DataMigrationFailure> Failures)> WriteAsync(DbContext db, List<Change> changes,
        ILogger logger, CancellationToken cancellationToken)
    {
        var sqlServer = IsSqlServer(db);
        if (sqlServer && changes.Count > 1)
        {
            try
            {
                return (await WriteSqlServerAsync(db, changes, logger, cancellationToken), []);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning("Data migration {Name} failed to write a batch ({Error}); writing it row by row",
                    Name, ex.GetType().Name);
            }
        }

        var written = 0;
        var failures = new List<DataMigrationFailure>();
        foreach (var change in changes)
        {
            try
            {
                written += sqlServer
                    ? await WriteSqlServerAsync(db, [change], logger, cancellationToken)
                    : await WriteRowAsync(db, change, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failures.Add(new(change.Id.ToString(), ex.GetType().Name));
            }
        }

        return (written, failures);
    }

    private async Task<int> WriteSqlServerAsync(DbContext db, List<Change> changes, ILogger logger,
        CancellationToken cancellationToken)
    {
        if (_sqlServerUpdate is null)
        {
            var type = await db.Database.SqlQueryRaw<string>(DataMigrationSql.ColumnType, Table, Column)
                .SingleAsync(cancellationToken);
            _sqlServerUpdate = Sql(db).UpdateFromJson(type);
            logger.LogInformation("Data migration {Name} writes batches with: {Sql}", Name, _sqlServerUpdate);
        }

        var json = JsonSerializer.Serialize(changes.Select(c => new { k = c.Id, n = c.After, o = c.Before }));
        return await db.Database.ExecuteSqlRawAsync(_sqlServerUpdate, [json], cancellationToken);
    }

    private Task<int> WriteRowAsync(DbContext db, Change change, CancellationToken cancellationToken)
    {
        var (sql, args) = Sql(db).UpdateRow(change.Id, change.Before, change.After);
        return db.Database.ExecuteSqlRawAsync(sql, args, cancellationToken);
    }

    private DataMigrationSql Sql(DbContext db) =>
        new(db.GetService<ISqlGenerationHelper>().DelimitIdentifier, IsSqlServer(db), Table, Key, Column);

    private static bool IsSqlServer(DbContext db) => db.Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer";

    private record Change(Guid Id, TValue Before, TValue After);
}
