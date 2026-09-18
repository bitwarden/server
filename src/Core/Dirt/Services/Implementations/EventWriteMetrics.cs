using System.Diagnostics.Metrics;

namespace Bit.Core.Dirt.Services.Implementations;

/// <summary>
/// Emits anonymous, aggregate metrics about event writes.
/// This class never records an organization identifier, a user identifier, or any other identifying value.
/// </summary>
public class EventWriteMetrics
{
    internal const string MeterName = "Bitwarden.Events";
    internal const string WriteFailureInstrumentName = "bitwarden.events.write_failures";
    internal const string DroppedInstrumentName = "bitwarden.events.dropped";

    private readonly Counter<long> _writeFailureCounter;
    private readonly Counter<long> _droppedCounter;

    public EventWriteMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _writeFailureCounter = meter.CreateCounter<long>(
            WriteFailureInstrumentName,
            unit: "{failures}",
            description: "Number of event write operations that failed and whose events were dropped.");
        _droppedCounter = meter.CreateCounter<long>(
            DroppedInstrumentName,
            unit: "{events}",
            description: "Number of individual events lost to a failed event write.");
    }

    /// <summary>
    /// Records one failed event write and the events lost with it.
    /// </summary>
    /// <param name="exceptionTypeName">
    /// The name of the exception type that failed the write. The set of possible values is small and fixed.
    /// It never carries an organization or a user identifier.
    /// </param>
    /// <param name="droppedCount">The number of events lost with this write.</param>
    public void RecordWriteFailure(string exceptionTypeName, int droppedCount)
    {
        var tag = new KeyValuePair<string, object?>("exception.type", exceptionTypeName);
        _writeFailureCounter.Add(1, tag);
        _droppedCounter.Add(droppedCount, tag);
    }
}
