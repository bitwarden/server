namespace Bit.Core
{
    public class GlobalSettings
    {
        public virtual string SiteName { get; set; }
        public virtual string BaseVaultUri { get; set; }
        public virtual string JwtSigningKey { get; set; }
        public virtual SqlServerSettings SqlServer { get; set; } = new SqlServerSettings();
        public virtual MailSettings Mail { get; set; } = new MailSettings();
        public virtual LoggrSettings Loggr { get; set; } = new LoggrSettings();
        public virtual CacheSettings Cache { get; set; } = new CacheSettings();
        public virtual PushSettings Push { get; set; } = new PushSettings();

        public class SqlServerSettings
        {
            public string ConnectionString { get; set; }
        }

        public class MailSettings
        {
            public string ApiKey { get; set; }
            public string ReplyToEmail { get; set; }
        }

        public class LoggrSettings
        {
            public string LogKey { get; set; }
            public string ApiKey { get; set; }
        }

        public class CacheSettings
        {
            public string ConnectionString { get; set; }
            public int Database { get; set; }
        }

        public class PushSettings
        {
            public string ApnsCertificateThumbprint { get; set; }
            public string ApnsCertificatePassword { get; set; }
            public string GcmSenderId { get; set; }
            public string GcmApiKey { get; set; }
            public string GcmAppPackageName { get; set; }
        }
    }
}
