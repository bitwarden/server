#nullable enable
using System.ComponentModel.DataAnnotations;
using Bit.Core.Entities;
using Bit.Core.Exceptions;

namespace Bit.Api.SecretManagerFeatures.Models.Request;

public class AccessPoliciesCreateRequest
{
    public IEnumerable<AccessPolicyRequest>? UserRequests { get; set; }

    public IEnumerable<AccessPolicyRequest>? GroupRequests { get; set; }

    public IEnumerable<AccessPolicyRequest>? ServiceAccountRequests { get; set; }

    public List<BaseAccessPolicy> ToBaseAccessPoliciesForProject(Guid projectId)
    {
        if (UserRequests == null && GroupRequests == null && ServiceAccountRequests == null)
        {
            throw new BadRequestException("No creation requests provided.");
        }

        var userAccessPolicies = UserRequests?
            .Select(x => x.ToUserProjectAccessPolicy(projectId)).ToList();

        var groupAccessPolicies = GroupRequests?
            .Select(x => x.ToGroupProjectAccessPolicy(projectId)).ToList();

        var serviceAccountAccessPolicies = ServiceAccountRequests?
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
    [Required] public Guid GranteeId { get; set; }

    [Required] public bool Read { get; set; }

    [Required] public bool Write { get; set; }

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
