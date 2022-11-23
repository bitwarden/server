using Serilog.Events;

namespace Bit.Core.Settings.LoggingSettings;

public class NotificationsLogLevelSettings : INotificationsLogLevelSettings
{
    public LogEventLevel Default { get; set; } = LogEventLevel.Information;
    public LogEventLevel IdentityToken { get; set; } = LogEventLevel.Fatal;
}
