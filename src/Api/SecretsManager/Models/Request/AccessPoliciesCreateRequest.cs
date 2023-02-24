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

    public IEnumerable<AccessPolicyRequest>? ProjectAccessPolicyRequests { get; set; }

    public List<BaseAccessPolicy> ToBaseAccessPoliciesForProject(Guid grantedProjectId)
    {
        if (UserAccessPolicyRequests == null && GroupAccessPolicyRequests == null && ServiceAccountAccessPolicyRequests == null)
        {
            throw new BadRequestException("No creation requests provided.");
        }

        var userAccessPolicies = UserAccessPolicyRequests?
            .Select(x => x.ToUserProjectAccessPolicy(grantedProjectId)).ToList();

        var groupAccessPolicies = GroupAccessPolicyRequests?
            .Select(x => x.ToGroupProjectAccessPolicy(grantedProjectId)).ToList();

        var serviceAccountAccessPolicies = ServiceAccountAccessPolicyRequests?
            .Select(x => x.ToServiceAccountProjectAccessPolicy(grantedProjectId)).ToList();

        var policies = new List<BaseAccessPolicy>();
        if (userAccessPolicies != null)
        {
            policies.AddRange(userAccessPolicies);
        }

        if (groupAccessPolicies != null)
        {
            policies.AddRange(groupAccessPolicies);
        }

        if (serviceAccountAccessPolicies != null)
        {
            policies.AddRange(serviceAccountAccessPolicies);
        }
        return policies;
    }

    public List<BaseAccessPolicy> ToBaseAccessPoliciesForServiceAccount(Guid grantedServiceAccountId)
    {
        if (UserAccessPolicyRequests == null && GroupAccessPolicyRequests == null && ProjectAccessPolicyRequests == null)
        {
            throw new BadRequestException("No creation requests provided.");
        }

        var userAccessPolicies = UserAccessPolicyRequests?
            .Select(x => x.ToUserServiceAccountAccessPolicy(grantedServiceAccountId)).ToList();

        var groupAccessPolicies = GroupAccessPolicyRequests?
            .Select(x => x.ToGroupServiceAccountAccessPolicy(grantedServiceAccountId)).ToList();

        var projectAccessPolicies = ProjectAccessPolicyRequests?
            .Select(x => x.ToProjectServiceAccountAccessPolicy(grantedServiceAccountId)).ToList();

        var policies = new List<BaseAccessPolicy>();
        if (userAccessPolicies != null)
        {
            policies.AddRange(userAccessPolicies);
        }

        if (groupAccessPolicies != null)
        {
            policies.AddRange(groupAccessPolicies);
        }

        if (projectAccessPolicies != null)
        {
            policies.AddRange(projectAccessPolicies);
        }

        return policies;
    }

    public int Count()
    {
        var total = 0;

        if (UserAccessPolicyRequests != null)
        {
            total += UserAccessPolicyRequests.Count();
        }
        if (GroupAccessPolicyRequests != null)
        {
            total += GroupAccessPolicyRequests.Count();
        }
        if (ServiceAccountAccessPolicyRequests != null)
        {
            total += ServiceAccountAccessPolicyRequests.Count();
        }

        return total;
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

    public UserServiceAccountAccessPolicy ToUserServiceAccountAccessPolicy(Guid id) =>
        new()
        {
            OrganizationUserId = GranteeId,
            GrantedServiceAccountId = id,
            Read = Read,
            Write = Write
        };

    public GroupServiceAccountAccessPolicy ToGroupServiceAccountAccessPolicy(Guid id) =>
        new()
        {
            GroupId = GranteeId,
            GrantedServiceAccountId = id,
            Read = Read,
            Write = Write
        };

    public ServiceAccountProjectAccessPolicy ToProjectServiceAccountAccessPolicy(Guid serviceAccountId) =>
        new()
        {
            ServiceAccountId = serviceAccountId,
            GrantedProjectId = GranteeId,
            Read = Read,
            Write = Write
        };
}
