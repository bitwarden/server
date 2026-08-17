using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Utilities;

namespace Bit.Core.Context;

/// <summary>
/// Represents the claims for a user in relation to a particular organization.
/// These claims will only be present for users in the <see cref="OrganizationUserStatusType.Confirmed"/> status.
/// </summary>
/// <remarks>
/// Implements <see cref="IOrganizationUserRole"/> so these claims can be used directly as the "acting user" in
/// <see cref="Bit.Core.AdminConsole.OrganizationFeatures.OrganizationUsers.OrganizationUserAction.IOrganizationUserValidationService"/>
/// checks, without mapping to an intermediate <see cref="OrganizationUserRole"/>.
/// </remarks>
public class CurrentContextOrganization : IOrganizationUserRole
{
    public CurrentContextOrganization() { }

    public CurrentContextOrganization(OrganizationUserOrganizationDetails orgUser)
    {
        Id = orgUser.OrganizationId;
        Type = orgUser.Type;
        Permissions = CoreHelpers.LoadClassFromJsonData<Permissions>(orgUser.Permissions);
        AccessSecretsManager = orgUser.AccessSecretsManager && orgUser.UseSecretsManager && orgUser.Enabled;
        AccessPam = orgUser.AccessPam && orgUser.UsePam && orgUser.Enabled;
    }

    public Guid Id { get; set; }
    public OrganizationUserType Type { get; set; }
    public Permissions Permissions { get; set; } = new();
    public bool AccessSecretsManager { get; set; }
    public bool AccessPam { get; set; }

    Guid IOrganizationUserRole.OrganizationId => Id;
    Permissions? IOrganizationUserRole.GetPermissions() => Permissions;
}
