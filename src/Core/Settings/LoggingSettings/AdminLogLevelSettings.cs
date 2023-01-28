using Serilog.Events;

namespace Bit.Core.Settings.LoggingSettings;

public class AdminLogLevelSettings : IAdminLogLevelSettings
{
    public LogEventLevel Default { get; set; } = LogEventLevel.Error;
}
