using System;
using Newtonsoft.Json;

namespace Bit.Scim.Models
{
    public class ScimMultiValuedAttribute
    {
        [JsonProperty("type")]
        public string Type { get; set; }
        [JsonProperty("primary")]
        public bool Primary { get; set; }
        [JsonProperty("display")]
        public string Display { get; set; }
        [JsonProperty("value")]
        public string Value { get; set; }
        [JsonProperty("$ref")]
        public Uri Ref { get; set; }
    }
}
