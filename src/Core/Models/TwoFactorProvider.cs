using System.Collections.Generic;

namespace Bit.Core.Models
{
    public class TwoFactorProvider
    {
        public bool Enabled { get; set; }
        public Dictionary<string, object> MetaData { get; set; } = new Dictionary<string, object>();

        public class U2fMetaData
        {
            public string KeyHandle { get; set; }
            public string PublicKey { get; set; }
            public string Certificate { get; set; }
            public uint Counter { get; set; }
            public bool Compromised { get; set; }
        }
    }
}
