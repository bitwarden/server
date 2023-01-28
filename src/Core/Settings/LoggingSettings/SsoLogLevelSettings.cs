using Serilog.Events;

namespace Bit.Core.Settings.LoggingSettings;

public class SsoLogLevelSettings : ISsoLogLevelSettings
{
    public LogEventLevel Default { get; set; } = LogEventLevel.Error;
}
