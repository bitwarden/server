using Serilog.Events;

namespace Bit.Core.Settings.LogSettings;

public class EventsProcessorLogLevelSettings : IEventsProcessorLogLevelSettings
{
    public LogEventLevel Default { get; set; } = LogEventLevel.Warning;
}
