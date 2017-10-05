using System;

namespace Bit.Core
{
    public class GlobalSettings
    {
        public bool SelfHosted { get; set; }
        public virtual string SiteName { get; set; }
        public virtual string StripeApiKey { get; set; }
        public virtual string ProjectName { get; set; }
        public virtual string LogDirectory { get; set; }
        public virtual string LicenseDirectory { get; set; }
        public virtual string PushRelayBaseUri { get; set; }
        public virtual bool DisableUserRegistration { get; set; }
        public virtual InstallationSettings Installation { get; set; } = new InstallationSettings();
        public virtual BaseServiceUriSettings BaseServiceUri { get; set; } = new BaseServiceUriSettings();
        public virtual SqlServerSettings SqlServer { get; set; } = new SqlServerSettings();
        public virtual MailSettings Mail { get; set; } = new MailSettings();
        public virtual StorageSettings Storage { get; set; } = new StorageSettings();
        public virtual AttachmentSettings Attachment { get; set; } = new AttachmentSettings();
        public virtual IdentityServerSettings IdentityServer { get; set; } = new IdentityServerSettings();
        public virtual DataProtectionSettings DataProtection { get; set; } = new DataProtectionSettings();
        public virtual DocumentDbSettings DocumentDb { get; set; } = new DocumentDbSettings();
        public virtual NotificationHubSettings NotificationHub { get; set; } = new NotificationHubSettings();
        public virtual YubicoSettings Yubico { get; set; } = new YubicoSettings();
        public virtual DuoSettings Duo { get; set; } = new DuoSettings();
        public virtual BraintreeSettings Braintree { get; set; } = new BraintreeSettings();

        public class BaseServiceUriSettings
        {
            public string Vault { get; set; }
            public string VaultWithHash => $"{Vault}/#";
            public string Api { get; set; }
            public string Identity { get; set; }
            public string InternalIdentity { get; set; }
        }

        public class SqlServerSettings
        {
            private string _connectionString;

            public string ConnectionString
            {
                get => _connectionString;
                set
                {
                    _connectionString = value.Trim('"');
                }
            }
        }

        public class StorageSettings
        {
            private string _connectionString;

            public string ConnectionString
            {
                get => _connectionString;
                set
                {
                    _connectionString = value.Trim('"');
                }
            }
        }

        public class AttachmentSettings
        {
            private string _connectionString;

            public string ConnectionString
            {
                get => _connectionString;
                set
                {
                    _connectionString = value.Trim('"');
                }
            }
            public string BaseDirectory { get; set; }
            public string BaseUrl { get; set; }
        }

        public class MailSettings
        {
            public string ReplyToEmail { get; set; }
            public string SendGridApiKey { get; set; }
            public SmtpSettings Smtp { get; set; } = new SmtpSettings();

            public class SmtpSettings
            {
                public string Host { get; set; }
                public int Port { get; set; } = 25;
                public bool Ssl { get; set; } = false;
                public string Username { get; set; }
                public string Password { get; set; }
                public bool UseDefaultCredentials { get; set; } = false;
            }
        }

        public class IdentityServerSettings
        {
            public string CertificateThumbprint { get; set; }
            public string CertificatePassword { get; set; }
        }

        public class DataProtectionSettings
        {
            public string CertificateThumbprint { get; set; }
            public string Directory { get; set; }
        }

        public class DocumentDbSettings
        {
            public string Uri { get; set; }
            public string Key { get; set; }
        }

        public class NotificationHubSettings
        {
            private string _connectionString;

            public string ConnectionString
            {
                get => _connectionString;
                set
                {
                    _connectionString = value.Trim('"');
                }
            }
            public string HubName { get; set; }
        }

        public class YubicoSettings
        {
            public string ClientId { get; set; }
            public string Key { get; set; }
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

        public class InstallationSettings
        {
            public Guid Id { get; set; }
            public string Key { get; set; }
            public string IdentityUri { get; set; }
        }
    }
}
