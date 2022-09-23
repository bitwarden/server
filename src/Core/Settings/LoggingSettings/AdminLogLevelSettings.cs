using Serilog.Events;

namespace Bit.Core.Settings.LogSettings;

public class AdminLogLevelSettings : IAdminLogLevelSettings
{
    public LogEventLevel Default { get; set; } = LogEventLevel.Error;
}
