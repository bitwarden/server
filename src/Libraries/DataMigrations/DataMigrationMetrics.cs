using System.Diagnostics.Metrics;
using System.Globalization;

namespace Bit.DataMigrations;

internal class DataMigrationMetrics
{
    internal const string MeterName = "Bitwarden.DataMigrations";

    private readonly Counter<long> _rows;
    private readonly Gauge<long> _pendingRows;
    private readonly Histogram<double> _batchDuration;

    public DataMigrationMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _rows = meter.CreateCounter<long>("bitwarden.datamigration.rows", "{rows}");
        _pendingRows = meter.CreateGauge<long>("bitwarden.datamigration.pending_rows", "{rows}");
        _batchDuration = meter.CreateHistogram<double>("bitwarden.datamigration.batch.duration", "s");
    }

    internal void RecordBatch(string name, int partition, bool dryRun, DataMigrationBatch batch, long pendingRows,
        TimeSpan duration)
    {
        var nameTag = KeyValuePair.Create("migration.name", (object?)name);
        var partitionTag = KeyValuePair.Create("migration.partition.name", (object?)partition.ToString(CultureInfo.InvariantCulture));
        var runTypeTag = KeyValuePair.Create("migration.run_type", (object?)(dryRun ? "DryRun" : "Live"));
        _rows.Add(batch.Converted, nameTag, partitionTag, runTypeTag, KeyValuePair.Create("migration.outcome", (object?)"Succeeded"));
        _rows.Add(batch.Skipped, nameTag, partitionTag, runTypeTag, KeyValuePair.Create("migration.outcome", (object?)"Skipped"));
        _rows.Add(batch.Failures.Count, nameTag, partitionTag, runTypeTag, KeyValuePair.Create("migration.outcome", (object?)"Failed"));
        _pendingRows.Record(Math.Max(0, pendingRows), nameTag, partitionTag, runTypeTag);
        _batchDuration.Record(duration.TotalSeconds, nameTag, partitionTag, runTypeTag);
    }
}
