// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Auth.Settings;
using Bit.Core.Settings.LoggingSettings;

namespace Bit.Core.Settings;

public class GlobalSettings : IGlobalSettings
{
    private string _mailTemplateDirectory;
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
    public bool LiteDeployment { get; set; }
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
    public virtual string MailTemplateDirectory
    {
        get => BuildDirectory(_mailTemplateDirectory, "/mail-templates");
        set => _mailTemplateDirectory = value;
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
    public virtual IInstallationSettings Installation { get; set; } = new InstallationSettings();
    public virtual IBaseServiceUriSettings BaseServiceUri { get; set; }
    public virtual string DatabaseProvider { get; set; }
    public virtual SqlSettings SqlServer { get; set; } = new SqlSettings();
    public virtual SqlSettings PostgreSql { get; set; } = new SqlSettings();
    public virtual SqlSettings MySql { get; set; } = new SqlSettings();
    public virtual SqlSettings Sqlite { get; set; } = new SqlSettings() { ConnectionString = "Data Source=:memory:" };
    public virtual SlackSettings Slack { get; set; } = new SlackSettings();
    public virtual TeamsSettings Teams { get; set; } = new TeamsSettings();
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
    public virtual IPhishingDomainSettings PhishingDomain { get; set; } = new PhishingDomainSettings();

    public virtual int SendAccessTokenLifetimeInMinutes { get; set; } = 5;
    public virtual bool EnableEmailVerification { get; set; }
    public virtual string KdfDefaultHashKey { get; set; }
    /// <summary>
    /// This Hash Key is used to prevent enumeration attacks against the Send Access feature.
    /// </summary>
    public virtual string SendDefaultHashKey { get; set; }
    public virtual string PricingUri { get; set; }
    public virtual Fido2Settings Fido2 { get; set; } = new Fido2Settings();

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
        private string _internalBilling;

        public BaseServiceUriSettings(GlobalSettings globalSettings)
        {
            _globalSettings = globalSettings;
        }

        public string CloudRegion { get; set; }
        public string Vault { get; set; }
        public string VaultWithHash => $"{Vault}/#";

        public string VaultWithHashAndSecretManagerProduct => $"{Vault}/#/sm";

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

        public string InternalBilling
        {
            get => _globalSettings.BuildInternalUri(_internalBilling, "billing");
            set => _internalBilling = value;
        }
    }

    public class SqlSettings
    {
        private string _connectionString;
        private string _readOnlyConnectionString;
        private string _jobSchedulerConnectionString;
        public bool SkipDatabasePreparation { get; set; }
        public bool DisableDatabaseMaintenanceJobs { get; set; }

        public string ConnectionString
        {
            get => _connectionString;
            set
            {
                // On development environment, the self-hosted overrides would not override the read-only connection string, since it is already set from the non-self-hosted connection string.
                // This causes a bug, where the read-only connection string is pointing to self-hosted database.
                if (!string.IsNullOrWhiteSpace(_readOnlyConnectionString) &&
                    _readOnlyConnectionString == _connectionString)
                {
                    _readOnlyConnectionString = null;
                }

                _connectionString = value.Trim('"');
            }
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

    public class SlackSettings
    {
        public virtual string ApiBaseUrl { get; set; } = "https://slack.com/api";
        public virtual string ClientId { get; set; }
        public virtual string ClientSecret { get; set; }
        public virtual string Scopes { get; set; }
    }

    public class TeamsSettings
    {
        public virtual string LoginBaseUrl { get; set; } = "https://login.microsoftonline.com";
        public virtual string GraphBaseUrl { get; set; } = "https://graph.microsoft.com/v1.0";
        public virtual string ClientId { get; set; }
        public virtual string ClientSecret { get; set; }
        public virtual string Scopes { get; set; }
    }

    public class EventLoggingSettings
    {
        public AzureServiceBusSettings AzureServiceBus { get; set; } = new AzureServiceBusSettings();
        public RabbitMqSettings RabbitMq { get; set; } = new RabbitMqSettings();
        public int IntegrationCacheRefreshIntervalMinutes { get; set; } = 10;
        public int MaxRetries { get; set; } = 3;

        public class AzureServiceBusSettings
        {
            private string _connectionString;
            private string _eventTopicName;
            private string _integrationTopicName;

            public virtual int DefaultMaxConcurrentCalls { get; set; } = 1;
            public virtual int DefaultPrefetchCount { get; set; } = 0;

            public virtual string EventRepositorySubscriptionName { get; set; } = "events-write-subscription";
            public virtual string SlackEventSubscriptionName { get; set; } = "events-slack-subscription";
            public virtual string SlackIntegrationSubscriptionName { get; set; } = "integration-slack-subscription";
            public virtual string WebhookEventSubscriptionName { get; set; } = "events-webhook-subscription";
            public virtual string WebhookIntegrationSubscriptionName { get; set; } = "integration-webhook-subscription";
            public virtual string HecEventSubscriptionName { get; set; } = "events-hec-subscription";
            public virtual string HecIntegrationSubscriptionName { get; set; } = "integration-hec-subscription";
            public virtual string DatadogEventSubscriptionName { get; set; } = "events-datadog-subscription";
            public virtual string DatadogIntegrationSubscriptionName { get; set; } = "integration-datadog-subscription";
            public virtual string TeamsEventSubscriptionName { get; set; } = "events-teams-subscription";
            public virtual string TeamsIntegrationSubscriptionName { get; set; } = "integration-teams-subscription";

            public string ConnectionString
            {
                get => _connectionString;
                set => _connectionString = value.Trim('"');
            }

            public string EventTopicName
            {
                get => _eventTopicName;
                set => _eventTopicName = value.Trim('"');
            }

            public string IntegrationTopicName
            {
                get => _integrationTopicName;
                set => _integrationTopicName = value.Trim('"');
            }
        }

        public class RabbitMqSettings
        {
            private string _hostName;
            private string _username;
            private string _password;
            private string _eventExchangeName;
            private string _integrationExchangeName;

            public int RetryTiming { get; set; } = 30000; // 30s
            public bool UseDelayPlugin { get; set; } = false;
            public virtual string EventRepositoryQueueName { get; set; } = "events-write-queue";
            public virtual string IntegrationDeadLetterQueueName { get; set; } = "integration-dead-letter-queue";
            public virtual string SlackEventsQueueName { get; set; } = "events-slack-queue";
            public virtual string SlackIntegrationQueueName { get; set; } = "integration-slack-queue";
            public virtual string SlackIntegrationRetryQueueName { get; set; } = "integration-slack-retry-queue";
            public virtual string WebhookEventsQueueName { get; set; } = "events-webhook-queue";
            public virtual string WebhookIntegrationQueueName { get; set; } = "integration-webhook-queue";
            public virtual string WebhookIntegrationRetryQueueName { get; set; } = "integration-webhook-retry-queue";
            public virtual string HecEventsQueueName { get; set; } = "events-hec-queue";
            public virtual string HecIntegrationQueueName { get; set; } = "integration-hec-queue";
            public virtual string HecIntegrationRetryQueueName { get; set; } = "integration-hec-retry-queue";
            public virtual string DatadogEventsQueueName { get; set; } = "events-datadog-queue";
            public virtual string DatadogIntegrationQueueName { get; set; } = "integration-datadog-queue";
            public virtual string DatadogIntegrationRetryQueueName { get; set; } = "integration-datadog-retry-queue";
            public virtual string TeamsEventsQueueName { get; set; } = "events-teams-queue";
            public virtual string TeamsIntegrationQueueName { get; set; } = "integration-teams-queue";
            public virtual string TeamsIntegrationRetryQueueName { get; set; } = "integration-teams-retry-queue";

            public string HostName
            {
                get => _hostName;
                set => _hostName = value.Trim('"');
            }
            public string Username
            {
                get => _username;
                set => _username = value.Trim('"');
            }
            public string Password
            {
                get => _password;
                set => _password = value.Trim('"');
            }
            public string EventExchangeName
            {
                get => _eventExchangeName;
                set => _eventExchangeName = value.Trim('"');
            }
            public string IntegrationExchangeName
            {
                get => _integrationExchangeName;
                set => _integrationExchangeName = value.Trim('"');
            }
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
        public string SendGridApiHost { get; set; } = "https://api.sendgrid.com";

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
        public string CertificateLocation { get; set; } = "identity.pfx";
        public string CertificateThumbprint { get; set; }
        public string CertificatePassword { get; set; }
        public string RedisConnectionString { get; set; }
        public string CosmosConnectionString { get; set; }
        public string LicenseKey { get; set; } = "eyJhbGciOiJQUzI1NiIsImtpZCI6IklkZW50aXR5U2VydmVyTGljZW5zZWtleS83Y2VhZGJiNzgxMzA0NjllODgwNjg5MTAyNTQxNGYxNiIsInR5cCI6ImxpY2Vuc2Urand0In0.eyJpc3MiOiJodHRwczovL2R1ZW5kZXNvZnR3YXJlLmNvbSIsImF1ZCI6IklkZW50aXR5U2VydmVyIiwiaWF0IjoxNzM0NTY2NDAwLCJleHAiOjE3NjQ5NzkyMDAsImNvbXBhbnlfbmFtZSI6IkJpdHdhcmRlbiBJbmMuIiwiY29udGFjdF9pbmZvIjoiY29udGFjdEBkdWVuZGVzb2Z0d2FyZS5jb20iLCJlZGl0aW9uIjoiU3RhcnRlciIsImlkIjoiNjg3OCIsImZlYXR1cmUiOlsiaXN2IiwidW5saW1pdGVkX2NsaWVudHMiXSwicHJvZHVjdCI6IkJpdHdhcmRlbiJ9.TYc88W_t2t0F2AJV3rdyKwGyQKrKFriSAzm1tWFNHNR9QizfC-8bliGdT4Wgeie-ynCXs9wWaF-sKC5emg--qS7oe2iIt67Qd88WS53AwgTvAddQRA4NhGB1R7VM8GAikLieSos-DzzwLYRgjZdmcsprItYGSJuY73r-7-F97ta915majBytVxGF966tT9zF1aYk0bA8FS6DcDYkr5f7Nsy8daS_uIUAgNa_agKXtmQPqKujqtUb6rgWEpSp4OcQcG-8Dpd5jHqoIjouGvY-5LTgk5WmLxi_m-1QISjxUJrUm-UGao3_VwV5KFGqYrz8csdTl-HS40ihWcsWnrV0ug";
        /// <summary>
        /// Sliding lifetime of a refresh token in seconds.
        ///
        /// Each time the refresh token is used before the sliding window ends, its lifetime is extended by another SlidingRefreshTokenLifetimeSeconds.
        ///
        /// If AbsoluteRefreshTokenLifetimeSeconds > 0, the sliding extensions are bounded by the absolute maximum lifetime.
        /// If SlidingRefreshTokenLifetimeSeconds = 0, sliding mode is invalid (refresh tokens cannot be used).
        /// </summary>
        public int? SlidingRefreshTokenLifetimeSeconds { get; set; }
        /// <summary>
        /// Maximum lifetime of a refresh token in seconds.
        ///
        /// Token cannot be refreshed by any means beyond the absolute refresh expiration.
        ///
        /// When setting this value to 0, the following effect applies:
        ///     If ApplyAbsoluteExpirationOnRefreshToken is set to true, the behavior is the same as when no refresh tokens are used.
        ///     If ApplyAbsoluteExpirationOnRefreshToken is set to false, refresh tokens only expire after the SlidingRefreshTokenLifetimeSeconds has passed.
        /// </summary>
        public int? AbsoluteRefreshTokenLifetimeSeconds { get; set; }
        /// <summary>
        /// Controls whether refresh tokens expire absolutely or on a sliding window basis.
        ///
        /// Absolute:
        ///     Token expires at a fixed point in time (defined by AbsoluteRefreshTokenLifetimeSeconds). Usage does not extend lifetime.
        ///
        /// Sliding(default):
        ///     Token lifetime is renewed on each use, by the amount in SlidingRefreshTokenLifetimeSeconds. Extensions stop once AbsoluteRefreshTokenLifetimeSeconds is reached (if set > 0).
        /// </summary>
        public bool ApplyAbsoluteExpirationOnRefreshToken { get; set; } = false;
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
        /// The file format of the certificate may be binary encoded (DER) or base64. If the private key is encrypted, provide the password in <see cref="CertificatePassword"/>,
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
            set => _connectionString = value?.Trim('"');
        }
        public string HubName { get; set; }
        /// <summary>
        /// Enables TestSend on the Azure Notification Hub, which allows tracing of the request through the hub and to the platform-specific push notification service (PNS).
        /// Enabling this will result in delayed responses because the Hub must wait on delivery to the PNS.  This should ONLY be enabled in a non-production environment, as results are throttled.
        /// </summary>
        public bool EnableSendTracing { get; set; } = false;
        /// <summary>
        /// The date and time at which registration will be enabled.
        ///
        /// **This value should not be updated once set, as it is used to determine installation location of devices.**
        ///
        /// If null, registration is disabled.
        ///
        /// </summary>
        public DateTime? RegistrationStartDate { get; set; }
        /// <summary>
        /// The date and time at which registration will be disabled.
        ///
        /// **This value should not be updated once set, as it is used to determine installation location of devices.**
        ///
        /// If null, hub registration has no yet known expiry.
        /// </summary>
        public DateTime? RegistrationEndDate { get; set; }
    }

    public class NotificationHubPoolSettings
    {
        /// <summary>
        /// List of Notification Hub settings to use for sending push notifications.
        ///
        /// Note that hubs on the same namespace share active device limits, so multiple namespaces should be used to increase capacity.
        /// </summary>
        public List<NotificationHubSettings> NotificationHubs { get; set; } = new();
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

    public class ImportCiphersLimitationSettings
    {
        public int CiphersLimit { get; set; }
        public int CollectionRelationshipsLimit { get; set; }
        public int CollectionsLimit { get; set; }
    }

    public class BitPaySettings
    {
        public bool Production { get; set; }
        public string Token { get; set; }
        public string NotificationUrl { get; set; }
        public string WebhookKey { get; set; }
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
        public string WebSiteInstanceId { get; set; }
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
        public bool EnforceSsoPolicyForAllUsers { get; set; }
    }

    public class StripeSettings
    {
        public string ApiKey { get; set; }
        public int MaxNetworkRetries { get; set; } = 2;
    }

    public class PhishingDomainSettings : IPhishingDomainSettings
    {
        public string UpdateUrl { get; set; }
        public string ChecksumUrl { get; set; }
    }

    public class DistributedIpRateLimitingSettings
    {
        public string RedisConnectionString { get; set; }
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
        public TimeSpan UserRequestExpiration { get; set; } = TimeSpan.FromMinutes(15);
        public TimeSpan AdminRequestExpiration { get; set; } = TimeSpan.FromDays(7);
        public TimeSpan AfterAdminApprovalExpiration { get; set; } = TimeSpan.FromHours(12);
    }

    public class DomainVerificationSettings : IDomainVerificationSettings
    {
        public int VerificationInterval { get; set; } = 12;
        public int ExpirationPeriod { get; set; } = 7;
    }

    public class LaunchDarklySettings : ILaunchDarklySettings
    {
        public string SdkKey { get; set; }
        public string FlagDataFilePath { get; set; } = "flags.json";
        public Dictionary<string, string> FlagValues { get; set; } = new Dictionary<string, string>();
    }

    public class DistributedCacheSettings
    {
        public virtual IConnectionStringSettings Redis { get; set; } = new ConnectionStringSettings();
        public virtual IConnectionStringSettings Cosmos { get; set; } = new ConnectionStringSettings();
        public ExtendedCacheSettings DefaultExtendedCache { get; set; } = new ExtendedCacheSettings();
    }

    /// <summary>
    /// A collection of Settings for customizing the FusionCache used in extended caching. Defaults are
    /// provided for every attribute so that only specific values need to be overridden if needed.
    /// </summary>
    public class ExtendedCacheSettings
    {
        public bool EnableDistributedCache { get; set; } = true;
        public bool UseSharedRedisCache { get; set; } = true;
        public IConnectionStringSettings Redis { get; set; } = new ConnectionStringSettings();
        public TimeSpan Duration { get; set; } = TimeSpan.FromMinutes(30);
        public bool IsFailSafeEnabled { get; set; } = true;
        public TimeSpan FailSafeMaxDuration { get; set; } = TimeSpan.FromHours(2);
        public TimeSpan FailSafeThrottleDuration { get; set; } = TimeSpan.FromSeconds(30);
        public float? EagerRefreshThreshold { get; set; } = 0.9f;
        public TimeSpan FactorySoftTimeout { get; set; } = TimeSpan.FromMilliseconds(100);
        public TimeSpan FactoryHardTimeout { get; set; } = TimeSpan.FromMilliseconds(1500);
        public TimeSpan DistributedCacheSoftTimeout { get; set; } = TimeSpan.FromSeconds(1);
        public TimeSpan DistributedCacheHardTimeout { get; set; } = TimeSpan.FromSeconds(2);
        public bool AllowBackgroundDistributedCacheOperations { get; set; } = true;
        public TimeSpan JitterMaxDuration { get; set; } = TimeSpan.FromSeconds(2);
        public TimeSpan DistributedCacheCircuitBreakerDuration { get; set; } = TimeSpan.FromSeconds(30);
    }

    public class WebPushSettings : IWebPushSettings
    {
        public string VapidPublicKey { get; set; }
    }

    public class Fido2Settings
    {
        public HashSet<string> Origins { get; set; }
    }
}
