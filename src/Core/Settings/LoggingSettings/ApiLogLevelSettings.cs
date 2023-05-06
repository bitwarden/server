using Serilog.Events;

namespace Bit.Core.Settings.LoggingSettings;

public class ApiLogLevelSettings : IApiLogLevelSettings
{
    public LogEventLevel Default { get; set; } = LogEventLevel.Debug;
    public LogEventLevel IdentityToken { get; set; } = LogEventLevel.Debug;
    public LogEventLevel IpRateLimit { get; set; } = LogEventLevel.Information;
}
