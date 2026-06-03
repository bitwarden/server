using System.Text.Json;
using Bit.Core.AdminConsole.Utilities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;

namespace Bit.Core.Context;

/// <summary>
/// Represents the claims for a user in relation to a particular organization.
/// These claims will only be present for users in the <see cref="OrganizationUserStatusType.Confirmed"/> status.
/// </summary>
public class CurrentContextOrganization
{
    public CurrentContextOrganization() { }

    public CurrentContextOrganization(OrganizationUserOrganizationDetails orgUser)
    {
        Id = orgUser.OrganizationId;
        Type = orgUser.Type;
        Permissions = string.IsNullOrWhiteSpace(orgUser.Permissions)
            ? new Permissions()
            : JsonSerializer.Deserialize(orgUser.Permissions, AdminConsoleJsonContext.Default.Permissions) ?? new Permissions();
        AccessSecretsManager = orgUser.AccessSecretsManager && orgUser.UseSecretsManager && orgUser.Enabled;
    }

    public Guid Id { get; set; }
    public OrganizationUserType Type { get; set; }
    public Permissions Permissions { get; set; } = new();
    public bool AccessSecretsManager { get; set; }
}
