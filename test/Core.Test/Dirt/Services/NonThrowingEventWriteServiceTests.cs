using System.Diagnostics.Metrics;
using Bit.Core.Dirt.Services.Implementations;
using Bit.Core.Models.Data;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture.Attributes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Bit.Core.Test.Dirt.Services;

public class NonThrowingEventWriteServiceTests
{
    private readonly IEventWriteService _inner = Substitute.For<IEventWriteService>();

    [Theory, BitAutoData]
    public async Task CreateAsync_PassesTheEventToTheInnerService(EventMessage eventMessage)
    {
        var subject = BuildSubject(out _);

        await subject.CreateAsync(eventMessage);

        await _inner.Received(1).CreateAsync(eventMessage);
    }

    [Theory, BitAutoData]
    public async Task CreateManyAsync_PassesTheEventsToTheInnerService(EventMessage eventMessage)
    {
        var subject = BuildSubject(out _);

        await subject.CreateManyAsync([eventMessage]);

        await _inner.Received(1).CreateManyAsync(Arg.Is<IEnumerable<IEvent>>(events => events.Single() == eventMessage));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_InnerServiceThrows_DoesNotThrow(EventMessage eventMessage)
    {
        _inner.CreateAsync(Arg.Any<IEvent>()).ThrowsAsync(new InvalidOperationException("topic is full"));
        var subject = BuildSubject(out _);

        await subject.CreateAsync(eventMessage);
    }

    [Theory, BitAutoData]
    public async Task CreateManyAsync_InnerServiceThrows_DoesNotThrow(EventMessage eventMessage)
    {
        _inner.CreateManyAsync(Arg.Any<IEnumerable<IEvent>>()).ThrowsAsync(new InvalidOperationException("topic is full"));
        var subject = BuildSubject(out _);

        await subject.CreateManyAsync([eventMessage]);
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_InnerServiceThrows_CountsOneDroppedEvent(EventMessage eventMessage)
    {
        _inner.CreateAsync(Arg.Any<IEvent>()).ThrowsAsync(new InvalidOperationException("topic is full"));
        var subject = BuildSubject(out var metrics);

        await subject.CreateAsync(eventMessage);

        Assert.Equal(1, TotalFor(metrics, EventWriteMetrics.WriteFailureInstrumentName));
        Assert.Equal(1, TotalFor(metrics, EventWriteMetrics.DroppedInstrumentName));
    }

    [Theory, BitAutoData]
    public async Task CreateManyAsync_InnerServiceThrows_CountsEveryEventInTheBatch(
        EventMessage first,
        EventMessage second,
        EventMessage third)
    {
        _inner.CreateManyAsync(Arg.Any<IEnumerable<IEvent>>()).ThrowsAsync(new InvalidOperationException("topic is full"));
        var subject = BuildSubject(out var metrics);

        await subject.CreateManyAsync([first, second, third]);

        Assert.Equal(1, TotalFor(metrics, EventWriteMetrics.WriteFailureInstrumentName));
        Assert.Equal(3, TotalFor(metrics, EventWriteMetrics.DroppedInstrumentName));
    }

    [Theory, BitAutoData]
    public async Task CreateManyAsync_InnerServiceThrows_EnumeratesADeferredSequenceOnlyOnce(EventMessage eventMessage)
    {
        _inner.CreateManyAsync(Arg.Any<IEnumerable<IEvent>>()).ThrowsAsync(new InvalidOperationException("topic is full"));
        var subject = BuildSubject(out var metrics);

        var enumerations = 0;
        IEnumerable<IEvent> Deferred()
        {
            enumerations++;
            yield return eventMessage;
        }

        await subject.CreateManyAsync(Deferred());

        Assert.Equal(1, enumerations);
        Assert.Equal(1, TotalFor(metrics, EventWriteMetrics.DroppedInstrumentName));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_InnerServiceSucceeds_CountsNothing(EventMessage eventMessage)
    {
        var subject = BuildSubject(out var metrics);

        await subject.CreateAsync(eventMessage);

        Assert.Equal(0, TotalFor(metrics, EventWriteMetrics.WriteFailureInstrumentName));
        Assert.Equal(0, TotalFor(metrics, EventWriteMetrics.DroppedInstrumentName));
    }

    [Theory, BitAutoData]
    public async Task CreateAsync_InnerServiceThrows_TagsTheFailureWithTheExceptionType(EventMessage eventMessage)
    {
        _inner.CreateAsync(Arg.Any<IEvent>()).ThrowsAsync(new TimeoutException());
        var subject = BuildSubject(out var metrics);

        await subject.CreateAsync(eventMessage);

        var measurement = Assert.Single(metrics[EventWriteMetrics.WriteFailureInstrumentName].GetMeasurementSnapshot());
        Assert.Equal(nameof(TimeoutException), measurement.Tags["exception.type"]);
    }

    private NonThrowingEventWriteService BuildSubject(out MetricSnapshot metrics)
    {
        var provider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var meterFactory = provider.GetRequiredService<IMeterFactory>();

        metrics = new MetricSnapshot(meterFactory);

        return new NonThrowingEventWriteService(
            _inner,
            new EventWriteMetrics(meterFactory),
            NullLogger<NonThrowingEventWriteService>.Instance);
    }

    private static long TotalFor(MetricSnapshot metrics, string instrumentName) =>
        metrics[instrumentName].GetMeasurementSnapshot().Sum(measurement => measurement.Value);

    // Collectors must exist before the instruments record, so both are created up front and kept alive
    // for the duration of the test.
    private sealed class MetricSnapshot
    {
        private readonly Dictionary<string, MetricCollector<long>> _collectors;

        public MetricSnapshot(IMeterFactory meterFactory)
        {
            _collectors = new Dictionary<string, MetricCollector<long>>
            {
                [EventWriteMetrics.WriteFailureInstrumentName] = new(
                    meterFactory, EventWriteMetrics.MeterName, EventWriteMetrics.WriteFailureInstrumentName),
                [EventWriteMetrics.DroppedInstrumentName] = new(
                    meterFactory, EventWriteMetrics.MeterName, EventWriteMetrics.DroppedInstrumentName)
            };
        }

        public MetricCollector<long> this[string instrumentName] => _collectors[instrumentName];
    }
}
