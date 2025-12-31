using System.Text.Json.Serialization;
using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.Dirt.Models.Data.EventIntegrations;

/// <summary>
/// Configuration for SCIM integration type.
/// </summary>
public class ScimIntegration
{
    /// <summary>
    /// Whether the SCIM integration is enabled.
    /// Replaces the separate Enabled column from OrganizationConnection.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The SCIM provider type (e.g., Okta, Azure AD, OneLogin).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ScimProviderType? ScimProvider { get; set; }
}
