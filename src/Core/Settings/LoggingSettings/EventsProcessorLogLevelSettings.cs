using Serilog.Events;

namespace Bit.Core.Settings.LoggingSettings;

public class EventsProcessorLogLevelSettings : IEventsProcessorLogLevelSettings
{
    public LogEventLevel Default { get; set; } = LogEventLevel.Warning;
}
