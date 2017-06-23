using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using u2flib.Util;

namespace Bit.Core.Models
{
    public class TwoFactorProvider
    {
        public bool Enabled { get; set; }
        public Dictionary<string, object> MetaData { get; set; } = new Dictionary<string, object>();

        public class U2fMetaData
        {
            public U2fMetaData() { }

            public U2fMetaData(dynamic o)
            {
                KeyHandle = o.KeyHandle;
                PublicKey = o.PublicKey;
                Certificate = o.Certificate;
                Counter = o.Counter;
                Compromised = o.Compromised;
            }

            public string KeyHandle { get; set; }
            [JsonIgnore]
            public byte[] KeyHandleBytes =>
                string.IsNullOrWhiteSpace(KeyHandle) ? null : Utils.Base64StringToByteArray(KeyHandle);
            public string PublicKey { get; set; }
            [JsonIgnore]
            public byte[] PublicKeyBytes =>
                string.IsNullOrWhiteSpace(PublicKey) ? null : Utils.Base64StringToByteArray(PublicKey);
            public string Certificate { get; set; }
            [JsonIgnore]
            public byte[] CertificateBytes =>
                string.IsNullOrWhiteSpace(Certificate) ? null : Utils.Base64StringToByteArray(Certificate);
            public uint Counter { get; set; }
            public bool Compromised { get; set; }
        }
    }
}
