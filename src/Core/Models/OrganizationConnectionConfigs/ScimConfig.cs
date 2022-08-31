using System.Text.Json.Serialization;
using Bit.Core.Enums;

namespace Bit.Core.Models.OrganizationConnectionConfigs;

public class ScimConfig
{
    public bool Enabled { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ScimProviderType? ScimProvider { get; set; }
}
