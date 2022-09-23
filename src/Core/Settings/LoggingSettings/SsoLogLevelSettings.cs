using Serilog.Events;

namespace Bit.Core.Settings.LogSettings;

public class SsoLogLevelSettings : ISsoLogLevelSettings
{
    public LogEventLevel Default { get; set; } = LogEventLevel.Error;
}
