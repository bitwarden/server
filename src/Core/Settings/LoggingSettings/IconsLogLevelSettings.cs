using Serilog.Events;

namespace Bit.Core.Settings.LogSettings;

public class IconsLogLevelSettings : IIconsLogLevelSettings
{
    public LogEventLevel Default { get; set; } = LogEventLevel.Error;
}
