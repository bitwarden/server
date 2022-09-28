using Serilog.Events;

namespace Bit.Core.Settings.LoggingSettings;

public class ScimLogLevelSettings : IScimLogLevelSettings
{
    public LogEventLevel Default { get; set; } = LogEventLevel.Warning;
}
