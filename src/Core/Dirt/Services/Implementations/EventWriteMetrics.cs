using System.Diagnostics.Metrics;
using Bit.Core.Models.Data;

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
    internal const string ExceptionTypeTagName = "exception.type";
    internal const string EventTypeTagName = "event.type";

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
            description: "Number of individual events lost to a failed event write, by event type.");
    }

    /// <summary>
    /// Records one failed event write and the events lost with it.
    /// </summary>
    /// <param name="exceptionTypeName">
    /// The name of the exception type that failed the write. The set of possible values is small and fixed.
    /// It never carries an organization or a user identifier.
    /// </param>
    /// <param name="droppedEvents">
    /// The events lost with this write. Only <see cref="IEvent.Type"/> is read, so the dropped count can be
    /// attributed to a bounded set of event types without recording anything that identifies a user,
    /// an organization, or the contents of the event.
    /// </param>
    public void RecordWriteFailure(string exceptionTypeName, IReadOnlyCollection<IEvent> droppedEvents)
    {
        var exceptionTag = new KeyValuePair<string, object?>(ExceptionTypeTagName, exceptionTypeName);
        _writeFailureCounter.Add(1, exceptionTag);

        foreach (var eventsOfType in droppedEvents.GroupBy(droppedEvent => droppedEvent.Type))
        {
            _droppedCounter.Add(
                eventsOfType.Count(),
                exceptionTag,
                new KeyValuePair<string, object?>(EventTypeTagName, eventsOfType.Key.ToString()));
        }
    }
}
