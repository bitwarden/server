using Bit.Core.Settings.LoggingSettings;

namespace Bit.Core.Settings;

public class GlobalSettings : IGlobalSettings
{
    private string _logDirectory;
    private string _licenseDirectory;

    public GlobalSettings()
    {
        BaseServiceUri = new BaseServiceUriSettings(this);
        Attachment = new FileStorageSettings(this, "attachments", "attachments");
        Send = new FileStorageSettings(this, "attachments/send", "attachments/send");
        DataProtection = new DataProtectionSettings(this);
    }

    public bool SelfHosted { get; set; }
    public virtual string KnownProxies { get; set; }
    public virtual string SiteName { get; set; }
    public virtual string ProjectName { get; set; }
    public virtual string LogDirectory
    {
        get => BuildDirectory(_logDirectory, "/logs");
        set => _logDirectory = value;
    }
    public virtual bool LogDirectoryByProject { get; set; } = true;
    public virtual long? LogRollBySizeLimit { get; set; }
    public virtual bool EnableDevLogging { get; set; } = false;
    public virtual string LicenseDirectory
    {
        get => BuildDirectory(_licenseDirectory, "/core/licenses");
        set => _licenseDirectory = value;
    }
    public string LicenseCertificatePassword { get; set; }
    public virtual string PushRelayBaseUri { get; set; }
    public virtual string InternalIdentityKey { get; set; }
    public virtual string OidcIdentityClientKey { get; set; }
    public virtual string HibpApiKey { get; set; }
    public virtual bool DisableUserRegistration { get; set; }
    public virtual bool DisableEmailNewDevice { get; set; }
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
    public virtual SqlSettings Sqlite { get; set; } = new SqlSettings();
    public virtual MailSettings Mail { get; set; } = new MailSettings();
    public virtual IConnectionStringSettings Storage { get; set; } = new ConnectionStringSettings();
    public virtual ConnectionStringSettings Events { get; set; } = new ConnectionStringSettings();
    public virtual IConnectionStringSettings Redis { get; set; } = new ConnectionStringSettings();
    public virtual NotificationsSettings Notifications { get; set; } = new NotificationsSettings();
    public virtual IFileStorageSettings Attachment { get; set; }
    public virtual FileStorageSettings Send { get; set; }
    public virtual IdentityServerSettings IdentityServer { get; set; } = new IdentityServerSettings();
    public virtual DataProtectionSettings DataProtection { get; set; }
    public virtual DocumentDbSettings DocumentDb { get; set; } = new DocumentDbSettings();
    public virtual SentrySettings Sentry { get; set; } = new SentrySettings();
    public virtual SyslogSettings Syslog { get; set; } = new SyslogSettings();
    public virtual ILogLevelSettings MinLogLevel { get; set; } = new LogLevelSettings();
    public virtual NotificationHubSettings NotificationHub { get; set; } = new NotificationHubSettings();
    public virtual YubicoSettings Yubico { get; set; } = new YubicoSettings();
    public virtual DuoSettings Duo { get; set; } = new DuoSettings();
    public virtual BraintreeSettings Braintree { get; set; } = new BraintreeSettings();
    public virtual BitPaySettings BitPay { get; set; } = new BitPaySettings();
    public virtual AmazonSettings Amazon { get; set; } = new AmazonSettings();
    public virtual ServiceBusSettings ServiceBus { get; set; } = new ServiceBusSettings();
    public virtual AppleIapSettings AppleIap { get; set; } = new AppleIapSettings();
    public virtual ISsoSettings Sso { get; set; } = new SsoSettings();
    public virtual StripeSettings Stripe { get; set; } = new StripeSettings();
    public virtual ITwoFactorAuthSettings TwoFactorAuth { get; set; } = new TwoFactorAuthSettings();
    public virtual DistributedIpRateLimitingSettings DistributedIpRateLimiting { get; set; } =
        new DistributedIpRateLimitingSettings();
    public virtual IPasswordlessAuthSettings PasswordlessAuth { get; set; } = new PasswordlessAuthSettings();

    public string BuildExternalUri(string explicitValue, string name)
    {
        if (!string.IsNullOrWhiteSpace(explicitValue))
        {
            return explicitValue;
        }
        if (!SelfHosted)
        {
            return null;
        }
        return string.Format("{0}/{1}", BaseServiceUri.Vault, name);
    }

    public string BuildInternalUri(string explicitValue, string name)
    {
        if (!string.IsNullOrWhiteSpace(explicitValue))
        {
            return explicitValue;
        }
        if (!SelfHosted)
        {
            return null;
        }
        return string.Format("http://{0}:5000", name);
    }

    public string BuildDirectory(string explicitValue, string appendedPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitValue))
        {
            return explicitValue;
        }
        if (!SelfHosted)
        {
            return null;
        }
        return string.Concat("/etc/bitwarden", appendedPath);
    }

    public class BaseServiceUriSettings : IBaseServiceUriSettings
    {
        private readonly GlobalSettings _globalSettings;

        private string _api;
        private string _identity;
        private string _admin;
        private string _notifications;
        private string _sso;
        private string _scim;
        private string _internalApi;
        private string _internalIdentity;
        private string _internalAdmin;
        private string _internalNotifications;
        private string _internalSso;
        private string _internalVault;
        private string _internalScim;

        public BaseServiceUriSettings(GlobalSettings globalSettings)
        {
            _globalSettings = globalSettings;
        }

        public string Vault { get; set; }
        public string VaultWithHash => $"{Vault}/#";

        public string Api
        {
            get => _globalSettings.BuildExternalUri(_api, "api");
            set => _api = value;
        }
        public string Identity
        {
            get => _globalSettings.BuildExternalUri(_identity, "identity");
            set => _identity = value;
        }
        public string Admin
        {
            get => _globalSettings.BuildExternalUri(_admin, "admin");
            set => _admin = value;
        }
        public string Notifications
        {
            get => _globalSettings.BuildExternalUri(_notifications, "notifications");
            set => _notifications = value;
        }
        public string Sso
        {
            get => _globalSettings.BuildExternalUri(_sso, "sso");
            set => _sso = value;
        }
        public string Scim
        {
            get => _globalSettings.BuildExternalUri(_scim, "scim");
            set => _scim = value;
        }

        public string InternalNotifications
        {
            get => _globalSettings.BuildInternalUri(_internalNotifications, "notifications");
            set => _internalNotifications = value;
        }
        public string InternalAdmin
        {
            get => _globalSettings.BuildInternalUri(_internalAdmin, "admin");
            set => _internalAdmin = value;
        }
        public string InternalIdentity
        {
            get => _globalSettings.BuildInternalUri(_internalIdentity, "identity");
            set => _internalIdentity = value;
        }
        public string InternalApi
        {
            get => _globalSettings.BuildInternalUri(_internalApi, "api");
            set => _internalApi = value;
        }
        public string InternalVault
        {
            get => _globalSettings.BuildInternalUri(_internalVault, "web");
            set => _internalVault = value;
        }
        public string InternalSso
        {
            get => _globalSettings.BuildInternalUri(_internalSso, "sso");
            set => _internalSso = value;
        }
        public string InternalScim
        {
            get => _globalSettings.BuildInternalUri(_scim, "scim");
            set => _internalScim = value;
        }
    }

    public class SqlSettings
    {
        private string _connectionString;
        private string _readOnlyConnectionString;
        private string _jobSchedulerConnectionString;

        public string ConnectionString
        {
            get => _connectionString;
            set => _connectionString = value.Trim('"');
        }

        public string ReadOnlyConnectionString
        {
            get => string.IsNullOrWhiteSpace(_readOnlyConnectionString) ?
                _connectionString : _readOnlyConnectionString;
            set => _readOnlyConnectionString = value.Trim('"');
        }

        public string JobSchedulerConnectionString
        {
            get => _jobSchedulerConnectionString;
            set => _jobSchedulerConnectionString = value.Trim('"');
        }
    }

    public class ConnectionStringSettings : IConnectionStringSettings
    {
        private string _connectionString;

        public string ConnectionString
        {
            get => _connectionString;
            set => _connectionString = value.Trim('"');
        }
    }

    public class FileStorageSettings : IFileStorageSettings
    {
        private readonly GlobalSettings _globalSettings;
        private readonly string _urlName;
        private readonly string _directoryName;
        private string _connectionString;
        private string _baseDirectory;
        private string _baseUrl;

        public FileStorageSettings(GlobalSettings globalSettings, string urlName, string directoryName)
        {
            _globalSettings = globalSettings;
            _urlName = urlName;
            _directoryName = directoryName;
        }

        public string ConnectionString
        {
            get => _connectionString;
            set => _connectionString = value.Trim('"');
        }

        public string BaseDirectory
        {
            get => _globalSettings.BuildDirectory(_baseDirectory, string.Concat("/core/", _directoryName));
            set => _baseDirectory = value;
        }

        public string BaseUrl
        {
            get => _globalSettings.BuildExternalUri(_baseUrl, _urlName);
            set => _baseUrl = value;
        }
    }

    public class MailSettings
    {
        private ConnectionStringSettings _connectionStringSettings;
        public string ConnectionString
        {
            get => _connectionStringSettings?.ConnectionString;
            set
            {
                if (_connectionStringSettings == null)
                {
                    _connectionStringSettings = new ConnectionStringSettings();
                }
                _connectionStringSettings.ConnectionString = value;
            }
        }
        public string ReplyToEmail { get; set; }
        public string AmazonConfigSetName { get; set; }
        public SmtpSettings Smtp { get; set; } = new SmtpSettings();
        public string SendGridApiKey { get; set; }
        public int? SendGridPercentage { get; set; }

        public class SmtpSettings
        {
            public string Host { get; set; }
            public int Port { get; set; } = 25;
            public bool StartTls { get; set; } = false;
            public bool Ssl { get; set; } = false;
            public bool SslOverride { get; set; } = false;
            public string Username { get; set; }
            public string Password { get; set; }
            public bool TrustServer { get; set; } = false;
        }
    }

    public class IdentityServerSettings
    {
        public string CertificateThumbprint { get; set; }
        public string CertificatePassword { get; set; }
        public string RedisConnectionString { get; set; }
    }

    public class DataProtectionSettings
    {
        private readonly GlobalSettings _globalSettings;

        private string _directory;

        public DataProtectionSettings(GlobalSettings globalSettings)
        {
            _globalSettings = globalSettings;
        }

        public string CertificateThumbprint { get; set; }
        public string CertificatePassword { get; set; }
        public string Directory
        {
            get => _globalSettings.BuildDirectory(_directory, "/core/aspnet-dataprotection");
            set => _directory = value;
        }
    }

    public class DocumentDbSettings
    {
        public string Uri { get; set; }
        public string Key { get; set; }
    }

    public class SentrySettings
    {
        public string Dsn { get; set; }
    }

    public class NotificationsSettings : ConnectionStringSettings
    {
        public string RedisConnectionString { get; set; }
    }

    public class SyslogSettings
    {
        /// <summary>
        /// The connection string used to connect to a remote syslog server over TCP or UDP, or to connect locally.
        /// </summary>
        /// <remarks>
        /// <para>The connection string will be parsed using <see cref="System.Uri" /> to extract the protocol, host name and port number.
        /// </para>
        /// <para>
        /// Supported protocols are:
        /// <list type="bullet">
        /// <item>UDP (use <code>udp://</code>)</item>
        /// <item>TCP (use <code>tcp://</code>)</item>
        /// <item>TLS over TCP (use <code>tls://</code>)</item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <example>
        /// A remote server (logging.dev.example.com) is listening on UDP (port 514):
        /// <code>
        /// udp://logging.dev.example.com:514</code>.
        /// </example>
        public string Destination { get; set; }
        /// <summary>
        /// The absolute path to a Certificate (DER or Base64 encoded with private key).
        /// </summary>
        /// <remarks>
        /// The certificate path and <see cref="CertificatePassword"/> are passed into the <see cref="System.Security.Cryptography.X509Certificates.X509Certificate2.X509Certificate2(string, string)" />.
        /// The file format of the certificate may be binary encded (DER) or base64. If the private key is encrypted, provide the password in <see cref="CertificatePassword"/>,
        /// </remarks>
        public string CertificatePath { get; set; }
        /// <summary>
        /// The password for the encrypted private key in the certificate supplied in <see cref="CertificatePath" />.
        /// </summary>
        /// <value></value>
        public string CertificatePassword { get; set; }
        /// <summary>
        /// The thumbprint of the certificate in the X.509 certificate store for personal certificates for the user account running Bitwarden. 
        /// </summary>
        /// <value></value>
        public string CertificateThumbprint { get; set; }
    }

    public class NotificationHubSettings
    {
        private string _connectionString;

        public string ConnectionString
        {
            get => _connectionString;
            set => _connectionString = value.Trim('"');
        }
        public string HubName { get; set; }

        /// <summary>
        /// Enables TestSend on the Azure Notification Hub, which allows tracing of the request through the hub and to the platform-specific push notification service (PNS).
        /// Enabling this will result in delayed responses because the Hub must wait on delivery to the PNS.  This should ONLY be enabled in a non-production environment, as results are throttled.
        /// </summary>
        public bool EnableSendTracing { get; set; } = false;
    }

    public class YubicoSettings
    {
        public string ClientId { get; set; }
        public string Key { get; set; }
        public string[] ValidationUrls { get; set; }
    }

    public class DuoSettings
    {
        public string AKey { get; set; }
    }

    public class BraintreeSettings
    {
        public bool Production { get; set; }
        public string MerchantId { get; set; }
        public string PublicKey { get; set; }
        public string PrivateKey { get; set; }
    }

    public class BitPaySettings
    {
        public bool Production { get; set; }
        public string Token { get; set; }
        public string NotificationUrl { get; set; }
    }

    public class InstallationSettings : IInstallationSettings
    {
        private string _identityUri;
        private string _apiUri;

        public Guid Id { get; set; }
        public string Key { get; set; }
        public string IdentityUri
        {
            get => string.IsNullOrWhiteSpace(_identityUri) ? "https://identity.bitwarden.com" : _identityUri;
            set => _identityUri = value;
        }
        public string ApiUri
        {
            get => string.IsNullOrWhiteSpace(_apiUri) ? "https://api.bitwarden.com" : _apiUri;
            set => _apiUri = value;
        }

    }

    public class AmazonSettings
    {
        public string AccessKeyId { get; set; }
        public string AccessKeySecret { get; set; }
        public string Region { get; set; }
    }

    public class ServiceBusSettings : ConnectionStringSettings
    {
        public string ApplicationCacheTopicName { get; set; }
        public string ApplicationCacheSubscriptionName { get; set; }
    }

    public class AppleIapSettings
    {
        public string Password { get; set; }
        public bool AppInReview { get; set; }
    }

    public class SsoSettings : ISsoSettings
    {
        public int CacheLifetimeInSeconds { get; set; } = 60;
        public double SsoTokenLifetimeInSeconds { get; set; } = 5;
    }

    public class CaptchaSettings
    {
        public bool ForceCaptchaRequired { get; set; } = false;
        public string HCaptchaSecretKey { get; set; }
        public string HCaptchaSiteKey { get; set; }
        public int MaximumFailedLoginAttempts { get; set; }
        public double MaybeBotScoreThreshold { get; set; } = double.MaxValue;
        public double IsBotScoreThreshold { get; set; } = double.MaxValue;
    }

    public class StripeSettings
    {
        public string ApiKey { get; set; }
        public int MaxNetworkRetries { get; set; } = 2;
    }

    public class TwoFactorAuthSettings : ITwoFactorAuthSettings
    {
        public bool EmailOnNewDeviceLogin { get; set; } = false;
    }

    public class DistributedIpRateLimitingSettings
    {
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Maximum number of Redis timeouts that can be experienced within the sliding timeout
        /// window before IP rate limiting is temporarily disabled.
        /// TODO: Determine/discuss a suitable maximum
        /// </summary>
        public int MaxRedisTimeoutsThreshold { get; set; } = 10;

        /// <summary>
        /// Length of the sliding window in seconds to track Redis timeout exceptions.
        /// TODO: Determine/discuss a suitable sliding window
        /// </summary>
        public int SlidingWindowSeconds { get; set; } = 120;
    }

    public class PasswordlessAuthSettings : IPasswordlessAuthSettings
    {
        public bool KnownDevicesOnly { get; set; } = true;
    }
}
