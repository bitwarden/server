using System;
using System.Collections.Generic;

namespace Bit.Core
{
    public class GlobalSettings
    {
        public string SiteName { get; set; }
        public string BaseVaultUri { get; set; }
        public virtual DocumentDBSettings DocumentDB { get; set; } = new DocumentDBSettings();
        public virtual MailSettings Mail { get; set; } = new MailSettings();

        public class DocumentDBSettings
        {
            public string Uri { get; set; }
            public string Key { get; set; }
            public string DatabaseId { get; set; }
            public string CollectionIdPrefix { get; set; }
            public int NumberOfCollections { get; set; }
        }

        public class MailSettings
        {
            public string APIKey { get; set; }
            public string ReplyToEmail { get; set; }
        }
    }
}
