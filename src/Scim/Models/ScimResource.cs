using Newtonsoft.Json;

namespace Bit.Scim.Models
{
    public abstract class ScimResource : ScimSchemaBase
    {
        [JsonProperty(Order = -5, PropertyName = "id")]
        public string Id { get; set; }
        [JsonProperty(PropertyName = "externalId")]
        public string ExternalId { get; set; }
        [JsonProperty(Order = 9999, PropertyName = "meta")]
        public ScimResourceMetadata Meta { get; set; }
    }
}
