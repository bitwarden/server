#nullable enable
using System.ComponentModel.DataAnnotations;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Api.SecretsManager.Models.Request;

public class AccessPoliciesCreateRequest
{
    public IEnumerable<AccessPolicyRequest>? UserAccessPolicyRequests { get; set; }

    public IEnumerable<AccessPolicyRequest>? GroupAccessPolicyRequests { get; set; }

    public IEnumerable<AccessPolicyRequest>? ServiceAccountAccessPolicyRequests { get; set; }

    public List<BaseAccessPolicy> ToBaseAccessPoliciesForProject(Guid projectId)
    {
        if (UserAccessPolicyRequests == null && GroupAccessPolicyRequests == null && ServiceAccountAccessPolicyRequests == null)
        {
            throw new BadRequestException("No creation requests provided.");
        }

        var userAccessPolicies = UserAccessPolicyRequests?
            .Select(x => x.ToUserProjectAccessPolicy(projectId)).ToList();

        var groupAccessPolicies = GroupAccessPolicyRequests?
            .Select(x => x.ToGroupProjectAccessPolicy(projectId)).ToList();

        var serviceAccountAccessPolicies = ServiceAccountAccessPolicyRequests?
            .Select(x => x.ToServiceAccountProjectAccessPolicy(projectId)).ToList();

        var policies = new List<BaseAccessPolicy>();
        if (userAccessPolicies != null) { policies.AddRange(userAccessPolicies); }
        if (groupAccessPolicies != null) { policies.AddRange(groupAccessPolicies); }
        if (serviceAccountAccessPolicies != null) { policies.AddRange(serviceAccountAccessPolicies); }
        return policies;
    }
}

public class AccessPolicyRequest
{
    [Required]
    public Guid GranteeId { get; set; }

    [Required]
    public bool Read { get; set; }

    [Required]
    public bool Write { get; set; }

    public UserProjectAccessPolicy ToUserProjectAccessPolicy(Guid projectId) =>
        new()
        {
            OrganizationUserId = GranteeId,
            GrantedProjectId = projectId,
            Read = Read,
            Write = Write
        };

    public GroupProjectAccessPolicy ToGroupProjectAccessPolicy(Guid projectId) =>
        new()
        {
            GroupId = GranteeId,
            GrantedProjectId = projectId,
            Read = Read,
            Write = Write
        };

    public ServiceAccountProjectAccessPolicy ToServiceAccountProjectAccessPolicy(Guid projectId) =>
        new()
        {
            ServiceAccountId = GranteeId,
            GrantedProjectId = projectId,
            Read = Read,
            Write = Write
        };
}
