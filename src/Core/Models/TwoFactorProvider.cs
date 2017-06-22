using System;
using System.Collections.Generic;
using U2F.Core.Utils;

namespace Bit.Core.Models
{
    public class TwoFactorProvider
    {
        public bool Enabled { get; set; }
        public Dictionary<string, object> MetaData { get; set; } = new Dictionary<string, object>();

        public class U2fMetaData
        {
            public string KeyHandle { get; set; }
            public byte[] KeyHandleBytes =>
                string.IsNullOrWhiteSpace(KeyHandle) ? null : Utils.Base64StringToByteArray(KeyHandle);
            public string PublicKey { get; set; }
            public byte[] PublicKeyBytes =>
                string.IsNullOrWhiteSpace(PublicKey) ? null : Utils.Base64StringToByteArray(PublicKey);
            public string Certificate { get; set; }
            public byte[] CertificateBytes =>
                string.IsNullOrWhiteSpace(Certificate) ? null : Utils.Base64StringToByteArray(Certificate);
            public uint Counter { get; set; }
            public bool Compromised { get; set; }
        }
    }
}
