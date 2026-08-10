#nullable enable

namespace Bit.Core.Jobs.DataMigrations;

/// <summary>
/// A gradual, resumable data migration executed by <see cref="DataMigrationsJob"/>. Implement via
/// <see cref="BaseDataMigration{TRow, TUpdate}"/>; register with
/// <c>services.AddTransient&lt;IDataMigration, MyMigration&gt;()</c> — the runner discovers all
/// registrations, so no per-migration scheduling exists.
/// </summary>
public interface IDataMigration
{
    /// <summary>Stable unique name: state-row key and telemetry tag. Never rename mid-flight.</summary>
    string Name { get; }

    /// <summary>Runs one round of batches (one per claimable partition, up to the migration's
    /// parallelism). Returns quietly when disabled, fully claimed elsewhere, or complete.</summary>
    Task RunAsync(CancellationToken token);
}
