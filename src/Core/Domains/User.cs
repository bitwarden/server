using System;
using Newtonsoft.Json;
using Bit.Core.Enums;

namespace Bit.Core.Domains
{
    public class User : IDataObject
    {
        internal static string TypeValue = "user";

        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        [JsonProperty("type")]
        public string Type { get; private set; } = TypeValue;

        public string Name { get; set; }
        public string Email { get; set; }
        public string MasterPassword { get; set; }
        public string MasterPasswordHint { get; set; }
        public string Culture { get; set; }
        public string SecurityStamp { get; set; }
        public string OldEmail { get; set; }
        public string OldMasterPassword { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public TwoFactorProvider? TwoFactorProvider { get; set; }
        public string AuthenticatorKey { get; set; }

        public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
    }
}
