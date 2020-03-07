using Bit.Core.Enums;
using Newtonsoft.Json;
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
            public U2fMetaData() { }

            public U2fMetaData(dynamic o)
            {
                Name = o.Name;
                KeyHandle = o.KeyHandle;
                PublicKey = o.PublicKey;
                Certificate = o.Certificate;
                Counter = o.Counter;
                Compromised = o.Compromised;
            }

            public string Name { get; set; }
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

        public static bool RequiresPremium(TwoFactorProviderType type)
        {
            switch(type)
            {
                case TwoFactorProviderType.Duo:
                case TwoFactorProviderType.YubiKey:
                case TwoFactorProviderType.U2f:
                    return true;
                default:
                    return false;
            }
        }
    }
}
