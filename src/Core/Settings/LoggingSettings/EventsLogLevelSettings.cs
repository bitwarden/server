using Serilog.Events;

namespace Bit.Core.Settings.LogSettings;

public class EventsLogLevelSettings : IEventsLogLevelSettings
{
    public LogEventLevel Default { get; set; } = LogEventLevel.Error;
    public LogEventLevel IdentityToken { get; set; } = LogEventLevel.Fatal;
}
