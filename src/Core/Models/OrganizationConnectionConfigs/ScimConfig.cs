using System.Text.Json.Serialization;
using Bit.Core.Enums;

namespace Bit.Core.Models.OrganizationConnectionConfigs;

public class ScimConfig : IConnectionConfig
{
    public bool Enabled { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ScimProviderType? ScimProvider { get; set; }

    public bool CanUse(out string exception)
    {
        if (!Enabled)
        {
            exception = "Scim Config is disabled";
            return false;
        }

        exception = "";
        return true;
    }
}
