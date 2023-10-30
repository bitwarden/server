#nullable enable
using System.ComponentModel.DataAnnotations;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Api.SecretsManager.Models.Request;

public class AccessPoliciesCreateRequest
{
    private static void CheckForDistinctAccessPolicies(IReadOnlyCollection<BaseAccessPolicy> accessPolicies)
    {
        var distinctAccessPolicies = accessPolicies.DistinctBy(baseAccessPolicy =>
        {
            return baseAccessPolicy switch
            {
                UserProjectAccessPolicy ap => new Tuple<Guid?, Guid?>(ap.OrganizationUserId, ap.GrantedProjectId),
                GroupProjectAccessPolicy ap => new Tuple<Guid?, Guid?>(ap.GroupId, ap.GrantedProjectId),
                ServiceAccountProjectAccessPolicy ap => new Tuple<Guid?, Guid?>(ap.ServiceAccountId,
                    ap.GrantedProjectId),
                UserServiceAccountAccessPolicy ap => new Tuple<Guid?, Guid?>(ap.OrganizationUserId,
                    ap.GrantedServiceAccountId),
                GroupServiceAccountAccessPolicy ap => new Tuple<Guid?, Guid?>(ap.GroupId, ap.GrantedServiceAccountId),
                _ => throw new ArgumentException("Unsupported access policy type provided.", nameof(baseAccessPolicy)),
            };
        }).ToList();

        if (accessPolicies.Count != distinctAccessPolicies.Count)
        {
            throw new BadRequestException("Resources must be unique");
        }
    }

    public IEnumerable<AccessPolicyRequest>? UserAccessPolicyRequests { get; set; }

    public IEnumerable<AccessPolicyRequest>? GroupAccessPolicyRequests { get; set; }

    public IEnumerable<AccessPolicyRequest>? ServiceAccountAccessPolicyRequests { get; set; }

    public List<BaseAccessPolicy> ToBaseAccessPoliciesForProject(Guid grantedProjectId, Guid organizationId)
    {
        if (UserAccessPolicyRequests == null && GroupAccessPolicyRequests == null && ServiceAccountAccessPolicyRequests == null)
        {
            throw new BadRequestException("No creation requests provided.");
        }

        var userAccessPolicies = UserAccessPolicyRequests?
            .Select(x => x.ToUserProjectAccessPolicy(grantedProjectId, organizationId)).ToList();

        var groupAccessPolicies = GroupAccessPolicyRequests?
            .Select(x => x.ToGroupProjectAccessPolicy(grantedProjectId, organizationId)).ToList();

        var serviceAccountAccessPolicies = ServiceAccountAccessPolicyRequests?
            .Select(x => x.ToServiceAccountProjectAccessPolicy(grantedProjectId, organizationId)).ToList();

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

        CheckForDistinctAccessPolicies(policies);
        return policies;
    }

    public List<BaseAccessPolicy> ToBaseAccessPoliciesForServiceAccount(Guid grantedServiceAccountId, Guid organizationId)
    {
        if (UserAccessPolicyRequests == null && GroupAccessPolicyRequests == null)
        {
            throw new BadRequestException("No creation requests provided.");
        }

        var userAccessPolicies = UserAccessPolicyRequests?
            .Select(x => x.ToUserServiceAccountAccessPolicy(grantedServiceAccountId, organizationId)).ToList();

        var groupAccessPolicies = GroupAccessPolicyRequests?
            .Select(x => x.ToGroupServiceAccountAccessPolicy(grantedServiceAccountId, organizationId)).ToList();

        var policies = new List<BaseAccessPolicy>();
        if (userAccessPolicies != null)
        {
            policies.AddRange(userAccessPolicies);
        }

        if (groupAccessPolicies != null)
        {
            policies.AddRange(groupAccessPolicies);
        }

        CheckForDistinctAccessPolicies(policies);
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

    public UserProjectAccessPolicy ToUserProjectAccessPolicy(Guid projectId, Guid organizationId) =>
        new()
        {
            OrganizationUserId = GranteeId,
            GrantedProjectId = projectId,
            GrantedProject = new Project { OrganizationId = organizationId, Id = projectId },
            Read = Read,
            Write = Write
        };

    public GroupProjectAccessPolicy ToGroupProjectAccessPolicy(Guid projectId, Guid organizationId) =>
        new()
        {
            GroupId = GranteeId,
            GrantedProjectId = projectId,
            GrantedProject = new Project { OrganizationId = organizationId, Id = projectId },
            Read = Read,
            Write = Write
        };

    public ServiceAccountProjectAccessPolicy ToServiceAccountProjectAccessPolicy(Guid projectId, Guid organizationId) =>
        new()
        {
            ServiceAccountId = GranteeId,
            GrantedProjectId = projectId,
            GrantedProject = new Project { OrganizationId = organizationId, Id = projectId },
            Read = Read,
            Write = Write
        };

    public UserServiceAccountAccessPolicy ToUserServiceAccountAccessPolicy(Guid id, Guid organizationId) =>
        new()
        {
            OrganizationUserId = GranteeId,
            GrantedServiceAccountId = id,
            GrantedServiceAccount = new ServiceAccount() { OrganizationId = organizationId, Id = id },
            Read = Read,
            Write = Write
        };

    public GroupServiceAccountAccessPolicy ToGroupServiceAccountAccessPolicy(Guid id, Guid organizationId) =>
        new()
        {
            GroupId = GranteeId,
            GrantedServiceAccountId = id,
            GrantedServiceAccount = new ServiceAccount() { OrganizationId = organizationId, Id = id },
            Read = Read,
            Write = Write
        };
}
