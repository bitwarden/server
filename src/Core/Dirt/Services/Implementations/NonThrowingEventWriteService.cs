using Bit.Core.Models.Data;
using Bit.Core.Services;
using Microsoft.Extensions.Logging;

namespace Bit.Core.Dirt.Services.Implementations;

/// <summary>
/// Wraps an <see cref="IEventWriteService"/> so that a failed write never reaches the caller. An event
/// records an operation, it is not part of one, and a request must not fail because its event could not
/// be written. A dropped write is reported through <see cref="EventWriteMetrics"/> and the log.
/// </summary>
public class NonThrowingEventWriteService : IEventWriteService
{
    private readonly IEventWriteService _inner;
    private readonly EventWriteMetrics _metrics;
    private readonly ILogger<NonThrowingEventWriteService> _logger;

    public NonThrowingEventWriteService(
        IEventWriteService inner,
        EventWriteMetrics metrics,
        ILogger<NonThrowingEventWriteService> logger)
    {
        _inner = inner;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task CreateAsync(IEvent e)
    {
        try
        {
            await _inner.CreateAsync(e);
        }
        catch (Exception ex)
        {
            RecordDroppedWrite(ex, droppedCount: 1);
        }
    }

    public async Task CreateManyAsync(IEnumerable<IEvent> events)
    {
        // Materialized so the dropped count is accurate and a deferred sequence is not enumerated twice
        var eventList = events as IList<IEvent> ?? events.ToList();

        try
        {
            await _inner.CreateManyAsync(eventList);
        }
        catch (Exception ex)
        {
            RecordDroppedWrite(ex, eventList.Count);
        }
    }

    private void RecordDroppedWrite(Exception ex, int droppedCount)
    {
        _metrics.RecordWriteFailure(ex.GetType().Name, droppedCount);

        // The count is safe to log; the events themselves are not
        _logger.LogError(ex, "Failed to write {DroppedCount} event(s). The events were dropped.", droppedCount);
    }
}
