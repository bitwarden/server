using Serilog.Events;

namespace Bit.Core.Settings.LoggingSettings;

public class BillingLogLevelSettings : IBillingLogLevelSettings
{
    public LogEventLevel Default { get; set; } = LogEventLevel.Warning;
    public LogEventLevel Jobs { get; set; } = LogEventLevel.Information;
}
