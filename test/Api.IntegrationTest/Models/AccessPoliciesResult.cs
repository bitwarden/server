namespace Bit.Api.IntegrationTest.Models;

public class AccessPoliciesResult
{
    public IEnumerable<UserProjectAccessPolicyResult> UserAccessPolicies { get; set; }

    public IEnumerable<GroupProjectAccessPolicyResult> GroupAccessPolicies { get; set; }

    public IEnumerable<ServiceAccountProjectAccessPolicyResult> ServiceAccountAccessPolicies { get; set; }

    public string Object { get; set; }
}

public class UserProjectAccessPolicyResult
{
    public string Object { get; set; }
    public Guid Id { get; set; }
    public bool Read { get; set; }
    public bool Write { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime RevisionDate { get; set; }
    public Guid? OrganizationUserId { get; set; }
    public string? OrganizationUserName { get; set; }
    public Guid? GrantedProjectId { get; set; }
}

public class GroupProjectAccessPolicyResult
{
    public string Object { get; set; }
    public Guid Id { get; set; }
    public bool Read { get; set; }
    public bool Write { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime RevisionDate { get; set; }
    public Guid? GroupId { get; set; }
    public string? GroupName { get; set; }
    public Guid? GrantedProjectId { get; set; }
}

public class ServiceAccountProjectAccessPolicyResult
{
    public string Object { get; set; }
    public Guid Id { get; set; }
    public bool Read { get; set; }
    public bool Write { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime RevisionDate { get; set; }
    public Guid? ServiceAccountId { get; set; }
    public string? ServiceAccountName { get; set; }
    public Guid? GrantedProjectId { get; set; }
}
