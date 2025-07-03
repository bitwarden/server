#nullable enable

using Bit.Core.Enums;

namespace Bit.Core.AdminConsole.Models.Data.Organizations.Policies;


public class UserPolicyDetails
{
    public Guid OrganizationId { get; set; }
    public string? PolicyData { get; set; }
    public OrganizationUserType OrganizationUserType { get; set; }
    public OrganizationUserStatusType OrganizationUserStatus { get; set; }

    public string? OrganizationUserPermissionsData { get; set; }
    /// <summary>
    /// True if the user is also a ProviderUser for the organization, false otherwise.
    /// </summary>
    public bool IsProvider { get; set; }

}
