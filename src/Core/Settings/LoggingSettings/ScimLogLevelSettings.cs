using Serilog.Events;

namespace Bit.Core.Settings.LogSettings;

public class ScimLogLevelSettings : IScimLogLevelSettings
{
    public LogEventLevel Default { get; set; } = LogEventLevel.Warning;
}
