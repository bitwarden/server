
namespace Bit.Core.Settings.LoggingSettings;

public class LogLevelSettings : ILogLevelSettings
{
    public IBillingLogLevelSettings BillingSettings { get; set; } = new BillingLogLevelSettings();
    public IApiLogLevelSettings ApiSettings { get; set; } = new ApiLogLevelSettings();
    public IIdentityLogLevelSettings IdentitySettings { get; set; } = new IdentityLogLevelSettings();
    public IScimLogLevelSettings ScimSettings { get; set; } = new ScimLogLevelSettings();
    public ISsoLogLevelSettings SsoSettings { get; set; } = new SsoLogLevelSettings();
    public IAdminLogLevelSettings AdminSettings { get; set; } = new AdminLogLevelSettings();
    public IEventsLogLevelSettings EventsSettings { get; set; } = new EventsLogLevelSettings();
    public IEventsProcessorLogLevelSettings EventsProcessorSettings { get; set; } = new EventsProcessorLogLevelSettings();
    public IIconsLogLevelSettings IconsSettings { get; set; } = new IconsLogLevelSettings();
    public INotificationsLogLevelSettings NotificationsSettings { get; set; } = new NotificationsLogLevelSettings();
}
