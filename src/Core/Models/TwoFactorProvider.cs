using Bit.Core.Enums;
using Fido2NetLib.Objects;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
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

        public class WebAuthnData
        {
            public WebAuthnData() { }

            public WebAuthnData(dynamic o)
            {
                Options = o.Options;
            }

            public string Options { get; set; }
            public PublicKeyCredentialDescriptor Descriptor { get; internal set; }
            public byte[] PublicKey { get; internal set; }
            public byte[] UserHandle { get; internal set; }
            public uint SignatureCounter { get; internal set; }
            public string CredType { get; internal set; }
            public DateTime RegDate { get; internal set; }
            public Guid AaGuid { get; internal set; }
        }

        public static bool RequiresPremium(TwoFactorProviderType type)
        {
            switch (type)
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
