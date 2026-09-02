using System.Text.Json.Serialization;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.Models.OrganizationConnectionConfigs;

namespace Bit.Core.AdminConsole.Models.OrganizationConnectionConfigs;

public class ScimConfig : IConnectionConfig
{
    public bool Enabled { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ScimProviderType? ScimProvider { get; set; }

    /// <summary>
    /// Determines whether newly provisioned users are sent an invitation email. When false, users are created in
    /// the Staged status without an invitation. Null (or missing from the stored config) is treated as true.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? InviteUsersAfterProvisioning { get; set; }

    public bool Validate(out string exception)
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
