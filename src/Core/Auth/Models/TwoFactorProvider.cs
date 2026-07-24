using System.Text.Json;
using Bit.Core.Auth.Enums;
using Bit.Core.Utilities;
using Fido2NetLib.Objects;

namespace Bit.Core.Auth.Models;

public class TwoFactorProvider
{
    public bool Enabled { get; set; }
    public Dictionary<string, object> MetaData { get; set; } = [];

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
                // byte[] Id used to be written as standard Base64 but now Fido2NetLib requires 
                // Base64Url encoding. We can't just deserialize straight into PublicKeyCredentialDescriptor. 
                // Extract and decode the Id manually instead.
                string descriptorJson = o.Descriptor.ToString();
                var descriptorElement = JsonDocument.Parse(descriptorJson).RootElement;
                var id = descriptorElement.EnumerateObject()
                    .First(p => string.Equals(p.Name, "Id", StringComparison.OrdinalIgnoreCase))
                    .Value.GetString() ?? throw new InvalidOperationException("Descriptor.Id is null");
                Descriptor = new PublicKeyCredentialDescriptor(CoreHelpers.Base64UrlDecode(id));
            }
            PublicKey = o.PublicKey;
            UserHandle = o.UserHandle;
            SignatureCounter = o.SignatureCounter;
            CredType = o.CredType;
            RegDate = o.RegDate;
            AaGuid = o.AaGuid;
            Migrated = o.Migrated;
        }

        public string? Name { get; set; }
        public PublicKeyCredentialDescriptor? Descriptor { get; internal set; }
        public byte[]? PublicKey { get; internal set; }
        public byte[]? UserHandle { get; internal set; }
        public uint SignatureCounter { get; set; }
        public string? CredType { get; internal set; }
        public DateTime? RegDate { get; internal set; }
        public Guid? AaGuid { get; internal set; }
        /// <summary>
        /// Migrated is used to track the transition between U2F and WebAuthn. 
        /// It is set to `true` for credentials created as U2F and have been migrated to WebAuthn.
        /// </summary>
        public bool Migrated { get; internal set; } = default;
    }

    public static bool RequiresPremium(TwoFactorProviderType type)
    {
        switch (type)
        {
            case TwoFactorProviderType.Duo:
            case TwoFactorProviderType.YubiKey:
                return true;
            default:
                return false;
        }
    }
}
