using Bit.Core.Auth.Settings;
using Bit.Core.Settings.LoggingSettings;

namespace Bit.Core.Settings;

public partial class GlobalSettings : IGlobalSettings
{
    private string _logDirectory;
    private string _licenseDirectory;

    public GlobalSettings()
    {
        BaseServiceUri = new BaseServiceUriSettings(this);
        Attachment = new FileStorageSettings(this, "attachments", "attachments");
        Send = new FileStorageSettings(this, "attachments/send", "attachments/send");
        DataProtection = new DataProtectionSettings(this);
        InfrastructureResourceProvider = new InfrastructureResourceProvider(this);
    }

    public bool SelfHosted { get; set; }
    public bool UnifiedDeployment { get; set; }
    public virtual string KnownProxies { get; set; }
    public virtual string SiteName { get; set; }
    public virtual string ProjectName { get; set; }
    public virtual string LogDirectory
    {
        get => InfrastructureResourceProvider.BuildDirectory(_logDirectory, "/logs");
        set => _logDirectory = value;
    }
    public virtual bool LogDirectoryByProject { get; set; } = true;
    public virtual long? LogRollBySizeLimit { get; set; }
    public virtual bool EnableDevLogging { get; set; } = false;
    public virtual string LicenseDirectory
    {
        get => InfrastructureResourceProvider.BuildDirectory(_licenseDirectory, "/core/licenses");
        set => _licenseDirectory = value;
    }
    public string LicenseCertificatePassword { get; set; }
    public virtual string PushRelayBaseUri { get; set; }
    public virtual string InternalIdentityKey { get; set; }
    public virtual string OidcIdentityClientKey { get; set; }
    public virtual string HibpApiKey { get; set; }
    public virtual bool DisableUserRegistration { get; set; }
    public virtual bool DisableEmailNewDevice { get; set; }
    public virtual bool EnableNewDeviceVerification { get; set; }
    public virtual bool EnableCloudCommunication { get; set; } = false;
    public virtual int OrganizationInviteExpirationHours { get; set; } = 120; // 5 days
    public virtual string EventGridKey { get; set; }
    public virtual CaptchaSettings Captcha { get; set; } = new CaptchaSettings();
    public virtual IInstallationSettings Installation { get; set; } = new InstallationSettings();
    public virtual IBaseServiceUriSettings BaseServiceUri { get; set; }
    public virtual string DatabaseProvider { get; set; }
    public virtual SqlSettings SqlServer { get; set; } = new SqlSettings();
    public virtual SqlSettings PostgreSql { get; set; } = new SqlSettings();
    public virtual SqlSettings MySql { get; set; } = new SqlSettings();
    public virtual SqlSettings Sqlite { get; set; } = new SqlSettings() { ConnectionString = "Data Source=:memory:" };
    public virtual EventLoggingSettings EventLogging { get; set; } = new EventLoggingSettings();
    public virtual MailSettings Mail { get; set; } = new MailSettings();
    public virtual IConnectionStringSettings Storage { get; set; } = new ConnectionStringSettings();
    public virtual ConnectionStringSettings Events { get; set; } = new ConnectionStringSettings();
    public virtual DistributedCacheSettings DistributedCache { get; set; } = new DistributedCacheSettings();
    public virtual NotificationsSettings Notifications { get; set; } = new NotificationsSettings();
    public virtual IFileStorageSettings Attachment { get; set; }
    public virtual FileStorageSettings Send { get; set; }
    public virtual IdentityServerSettings IdentityServer { get; set; } = new IdentityServerSettings();
    public virtual DataProtectionSettings DataProtection { get; set; }
    public virtual SentrySettings Sentry { get; set; } = new SentrySettings();
    public virtual SyslogSettings Syslog { get; set; } = new SyslogSettings();
    public virtual ILogLevelSettings MinLogLevel { get; set; } = new LogLevelSettings();
    public virtual NotificationHubPoolSettings NotificationHubPool { get; set; } = new();
    public virtual YubicoSettings Yubico { get; set; } = new YubicoSettings();
    public virtual DuoSettings Duo { get; set; } = new DuoSettings();
    public virtual BraintreeSettings Braintree { get; set; } = new BraintreeSettings();
    public virtual ImportCiphersLimitationSettings ImportCiphersLimitation { get; set; } = new ImportCiphersLimitationSettings();
    public virtual BitPaySettings BitPay { get; set; } = new BitPaySettings();
    public virtual AmazonSettings Amazon { get; set; } = new AmazonSettings();
    public virtual ServiceBusSettings ServiceBus { get; set; } = new ServiceBusSettings();
    public virtual AppleIapSettings AppleIap { get; set; } = new AppleIapSettings();
    public virtual ISsoSettings Sso { get; set; } = new SsoSettings();
    public virtual StripeSettings Stripe { get; set; } = new StripeSettings();
    public virtual DistributedIpRateLimitingSettings DistributedIpRateLimiting { get; set; } =
        new DistributedIpRateLimitingSettings();
    public virtual IPasswordlessAuthSettings PasswordlessAuth { get; set; } = new PasswordlessAuthSettings();
    public virtual IDomainVerificationSettings DomainVerification { get; set; } = new DomainVerificationSettings();
    public virtual ILaunchDarklySettings LaunchDarkly { get; set; } = new LaunchDarklySettings();
    public virtual string DevelopmentDirectory { get; set; }
    public virtual IWebPushSettings WebPush { get; set; } = new WebPushSettings();

    public virtual bool EnableEmailVerification { get; set; }
    public virtual string KdfDefaultHashKey { get; set; }
    public virtual string PricingUri { get; set; }
    public virtual IInfrastructureResourceProvider InfrastructureResourceProvider { get; set; }
}
