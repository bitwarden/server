using System;
using System.Collections.Generic;

namespace Bit.Core
{
    public class GlobalSettings
    {
        public virtual string SiteName { get; set; }
        public virtual string BaseVaultUri { get; set; }
        public virtual string JwtSigningKey { get; set; }
        public virtual DocumentDBSettings DocumentDB { get; set; } = new DocumentDBSettings();
        public virtual SqlServerSettings SqlServer { get; set; } = new SqlServerSettings();
        public virtual MailSettings Mail { get; set; } = new MailSettings();
        public virtual LoggrSettings Loggr { get; set; } = new LoggrSettings();

        public class DocumentDBSettings
        {
            public string Uri { get; set; }
            public string Key { get; set; }
            public string DatabaseId { get; set; }
            public string CollectionIdPrefix { get; set; }
            public int NumberOfCollections { get; set; }
        }

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
    }
}
