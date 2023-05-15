using Bit.Core.Enums;

namespace Bit.Core.Models.Data.Organizations.OrganizationUsers;

public class OrganizationUserPolicyDetails
{
    public Guid OrganizationUserId { get; set; }

    public Guid OrganizationId { get; set; }

    public PolicyType PolicyType { get; set; }

    public bool PolicyEnabled { get; set; }

    public string PolicyData { get; set; }

    public OrganizationUserType OrganizationUserType { get; set; }

    public OrganizationUserStatusType OrganizationUserStatus { get; set; }

    public string OrganizationUserPermissionsData { get; set; }

    public bool IsProvider { get; set; }
}
