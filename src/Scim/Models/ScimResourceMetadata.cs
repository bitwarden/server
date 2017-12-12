using Newtonsoft.Json;

namespace Bit.Scim.Models
{
    public class ScimResourceMetadata
    {
        private ScimResourceMetadata() { }

        public ScimResourceMetadata(string resourceType)
        {
            ResourceType = resourceType;
        }

        [JsonProperty("resourceType")]
        public string ResourceType { get; set; }
    }
}
