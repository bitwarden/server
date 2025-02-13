#nullable enable

using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.Models.Data.Organizations.Policies;

/// <summary>
/// Represents an OrganizationUser and a Policy which *may* be enforced against them.
/// You may assume that the Policy is enabled and that the organization's plan supports policies.
/// This is consumed by <see cref="IPolicyRequirement"/> to create requirements for specific policy types.
/// </summary>
public class PolicyDetails
{
    public Guid OrganizationUserId { get; set; }
    public Guid OrganizationId { get; set; }
    public PolicyType PolicyType { get; set; }
    public string? PolicyData { get; set; }
    public OrganizationUserType OrganizationUserType { get; set; }
    public OrganizationUserStatusType OrganizationUserStatus { get; set; }
    /// <summary>
    /// Custom permissions for the organization user, if any. Use <see cref="GetOrganizationUserCustomPermissions"/>
    /// to deserialize.
    /// </summary>
    public string? OrganizationUserPermissionsData { get; set; }
    /// <summary>
    /// True if the user is also a ProviderUser for the organization, false otherwise.
    /// </summary>
    public bool IsProvider { get; set; }

    public T GetDataModel<T>() where T : IPolicyDataModel, new()
        => CoreHelpers.LoadClassFromJsonData<T>(PolicyData);

    public Permissions GetOrganizationUserCustomPermissions()
        => CoreHelpers.LoadClassFromJsonData<Permissions>(OrganizationUserPermissionsData);
}
