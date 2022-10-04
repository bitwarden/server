using Serilog.Events;

namespace Bit.Core.Settings.LoggingSettings;

public class IconsLogLevelSettings : IIconsLogLevelSettings
{
    public LogEventLevel Default { get; set; } = LogEventLevel.Error;
}
