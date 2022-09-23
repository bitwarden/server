using Serilog.Events;

namespace Bit.Core.Settings;

public interface ILogLevelSettings
{
    IBillingLogLevelSettings BillingSettings { get; set; }
    IApiLogLevelSettings ApiSettings { get; set; }
    IIdentityLogLevelSettings IdentitySettings { get; set; }
    IScimLogLevelSettings ScimSettings { get; set; }
    ISsoLogLevelSettings SsoSettings { get; set; }
    IAdminLogLevelSettings AdminSettings { get; set; }
    IEventsLogLevelSettings EventsSettings { get; set; }
    IEventsProcessorLogLevelSettings EventsProcessorSettings { get; set; }
    IIconsLogLevelSettings IconsSettings { get; set; }
    INotificationsLogLevelSettings NotificationsSettings { get; set; }
}

public interface IBillingLogLevelSettings
{
    LogEventLevel Default { get; set; }
    LogEventLevel Jobs { get; set; }
}

public interface IApiLogLevelSettings
{
    LogEventLevel Default { get; set; }
    LogEventLevel IdentityToken { get; set; }
    LogEventLevel IpRateLimit { get; set; }
}

public interface IIdentityLogLevelSettings
{
    LogEventLevel Default { get; set; }
    LogEventLevel IdentityToken { get; set; }
    LogEventLevel IpRateLimit { get; set; }
}

public interface IScimLogLevelSettings
{
    LogEventLevel Default { get; set; }
}

public interface ISsoLogLevelSettings
{
    LogEventLevel Default { get; set; }
}

public interface IAdminLogLevelSettings
{
    LogEventLevel Default { get; set; }
}

public interface IEventsLogLevelSettings
{
    LogEventLevel Default { get; set; }
    LogEventLevel IdentityToken { get; set; }
}

public interface IEventsProcessorLogLevelSettings
{
    LogEventLevel Default { get; set; }
}

public interface IIconsLogLevelSettings
{
    LogEventLevel Default { get; set; }
}

public interface INotificationsLogLevelSettings
{
    LogEventLevel Default { get; set; }
    LogEventLevel IdentityToken { get; set; }
}
