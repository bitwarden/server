using System.Diagnostics.Metrics;

namespace Bit.Api.Platform.Sync;

public sealed class SyncMetrics
{
    private readonly Histogram<int> _syncVaultCount;

    public SyncMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Bitwarden.Sync");
        _syncVaultCount = meter.CreateHistogram<int>(
            "bitwarden.sync.vault_count",
            unit: "{item}",
            description: "The number of ciphers returned in the sync operation."
        );
    }

    public void RecordSyncInfo(int cipherCount)
    {
        _syncVaultCount.Record(cipherCount);
    }
}
