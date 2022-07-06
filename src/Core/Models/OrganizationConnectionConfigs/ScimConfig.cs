using System.Text.Json.Serialization;
using Bit.Core.Enums;

namespace Bit.Core.Models.OrganizationConnectionConfigs
{
    public class ScimConfig
    {
        public bool Enabled { get; set; }
        public ScimProviderType? ScimProvider { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ServiceUrl { get; set; }
    }
}
