using Bit.Core.Auth.Settings;

namespace Bit.Core.Settings;

public interface IGlobalSettings
{
    // This interface exists for testing. Add settings here as needed for testing
    bool SelfHosted { get; set; }
    bool LiteDeployment { get; set; }
    string KnownProxies { get; set; }
    string ProjectName { get; set; }
    bool EnableCloudCommunication { get; set; }
    string LicenseDirectory { get; set; }
    string LicenseCertificatePassword { get; set; }
    int OrganizationInviteExpirationHours { get; set; }
    bool DisableUserRegistration { get; set; }
    bool EnableNewDeviceVerification { get; set; }
    IInstallationSettings Installation { get; set; }
    IFileStorageSettings Attachment { get; set; }
    IConnectionStringSettings Storage { get; set; }
    IBaseServiceUriSettings BaseServiceUri { get; set; }
    ISsoSettings Sso { get; set; }
    ILogLevelSettings MinLogLevel { get; set; }
    IPasswordlessAuthSettings PasswordlessAuth { get; set; }
    IDomainVerificationSettings DomainVerification { get; set; }
    ILaunchDarklySettings LaunchDarkly { get; set; }
    string DatabaseProvider { get; set; }
    GlobalSettings.SqlSettings SqlServer { get; set; }
    string DevelopmentDirectory { get; set; }
    IWebPushSettings WebPush { get; set; }
    GlobalSettings.EventLoggingSettings EventLogging { get; set; }
    IPhishingDomainSettings PhishingDomain { get; set; }
}
