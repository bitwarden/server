using Bit.Core.Enums;
using Fido2NetLib.Objects;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PeterO.Cbor;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
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

            private static CBORObject CreatePublicKeyFromU2fRegistrationData(byte[] keyHandleData, byte[] publicKeyData)
            {
                var x = new byte[32];
                var y = new byte[32];
                Buffer.BlockCopy(publicKeyData, 1, x, 0, 32);
                Buffer.BlockCopy(publicKeyData, 33, y, 0, 32);

                var point = new ECPoint
                {
                    X = x,
                    Y = y,
                };

                var coseKey = CBORObject.NewMap();

                coseKey.Add(COSE.KeyCommonParameter.KeyType, COSE.KeyType.EC2);
                coseKey.Add(COSE.KeyCommonParameter.Alg, -7);

                coseKey.Add(COSE.KeyTypeParameter.Crv, COSE.EllipticCurve.P256);

                coseKey.Add(COSE.KeyTypeParameter.X, point.X);
                coseKey.Add(COSE.KeyTypeParameter.Y, point.Y);

                return coseKey;
            }

            public WebAuthnData ToWebAuthnData()
            {
                return new WebAuthnData
                {
                    Name = Name,
                    Descriptor = new PublicKeyCredentialDescriptor
                    {
                        Id = KeyHandleBytes,
                        Type = PublicKeyCredentialType.PublicKey
                    },
                    PublicKey = CreatePublicKeyFromU2fRegistrationData(KeyHandleBytes, PublicKeyBytes).EncodeToBytes(),
                    SignatureCounter = Counter,
                    Migrated = true,
                };
            }
        }

        public class WebAuthnData
        {
            public WebAuthnData() { }

            public WebAuthnData(dynamic o)
            {
                Name = o.Name;
                try
                {
                    Descriptor = o.Descriptor;
                }
                catch
                {
                    // Handle newtonsoft parsing
                    Descriptor = JsonConvert.DeserializeObject<PublicKeyCredentialDescriptor>(o.Descriptor.ToString());
                }
                PublicKey = o.PublicKey;
                UserHandle = o.UserHandle;
                SignatureCounter = o.SignatureCounter;
                CredType = o.CredType;
                RegDate = o.RegDate;
                AaGuid = o.AaGuid;
                Migrated = o.Migrated;
            }

            public string Name { get; set; }
            public PublicKeyCredentialDescriptor Descriptor { get; internal set; }
            public byte[] PublicKey { get; internal set; }
            public byte[] UserHandle { get; internal set; }
            public uint SignatureCounter { get; set; }
            public string CredType { get; internal set; }
            public DateTime RegDate { get; internal set; }
            public Guid AaGuid { get; internal set; }
            public bool Migrated { get; internal set; }
        }

        public static bool RequiresPremium(TwoFactorProviderType type)
        {
            switch (type)
            {
                case TwoFactorProviderType.Duo:
                case TwoFactorProviderType.YubiKey:
                case TwoFactorProviderType.U2f:
                case TwoFactorProviderType.WebAuthn:
                    return true;
                default:
                    return false;
            }
        }
    }
}
