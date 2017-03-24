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
        public virtual PushSettings Push { get; set; } = new PushSettings();
        public virtual StorageSettings Storage { get; set; } = new StorageSettings();
        public virtual IdentityServerSettings IdentityServer { get; set; } = new IdentityServerSettings();
        public virtual DataProtectionSettings DataProtection { get; set; } = new DataProtectionSettings();
        public virtual DocumentDbSettings DocumentDb { get; set; } = new DocumentDbSettings();

        public class SqlServerSettings
        {
            public string ConnectionString { get; set; }
        }

        public class StorageSettings
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

        public class PushSettings
        {
            public string ApnsCertificateThumbprint { get; set; }
            public string ApnsCertificatePassword { get; set; }
            public string GcmSenderId { get; set; }
            public string GcmApiKey { get; set; }
            public string GcmAppPackageName { get; set; }
        }

        public class IdentityServerSettings
        {
            public string CertificateThumbprint { get; set; }
        }

        public class DataProtectionSettings
        {
            public string CertificateThumbprint { get; set; }
        }

        public class DocumentDbSettings
        {
            public string Uri { get; set; }
            public string Key { get; set; }
        }
    }
}
