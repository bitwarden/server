using System;
using Newtonsoft.Json;
using Bit.Core.Enums;

namespace Bit.Core.Domains
{
    public abstract class Cipher : IDataObject
    {
        internal static string TypeValue = "cipher";

        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("type")]
        public string Type { get; private set; } = TypeValue;
        public abstract CipherType CipherType { get; protected set; }

        public string UserId { get; set; }
        public string Name { get; set; }
        public bool Dirty { get; set; }
        public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
        public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;
    }
}
