#nullable enable
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Enums.AccessPolicies;
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;

namespace Bit.Core.SecretsManager.Models.Data;

public class ServiceAccountGrantedPolicies
{
    public ServiceAccountGrantedPolicies(Guid serviceAccountId, IEnumerable<BaseAccessPolicy> policies)
    {
        ServiceAccountId = serviceAccountId;
        ProjectGrantedPolicies = policies.Where(x => x is ServiceAccountProjectAccessPolicy)
            .Cast<ServiceAccountProjectAccessPolicy>().ToList();

        var serviceAccount = ProjectGrantedPolicies.FirstOrDefault()?.ServiceAccount;
        if (serviceAccount != null)
        {
            OrganizationId = serviceAccount.OrganizationId;
        }
    }

    public ServiceAccountGrantedPolicies()
    {
    }

    public Guid ServiceAccountId { get; set; }
    public Guid OrganizationId { get; set; }

    public IEnumerable<ServiceAccountProjectAccessPolicy> ProjectGrantedPolicies { get; set; } =
        new List<ServiceAccountProjectAccessPolicy>();

    public ServiceAccountGrantedPoliciesUpdates GetPolicyUpdates(ServiceAccountGrantedPolicies requested)
    {
        var currentProjectIds = ProjectGrantedPolicies.Select(p => p.GrantedProjectId!.Value).ToList();
        var requestedProjectIds = requested.ProjectGrantedPolicies.Select(p => p.GrantedProjectId!.Value).ToList();

        var projectIdsToBeDeleted = currentProjectIds.Except(requestedProjectIds).ToList();
        var projectIdsToBeCreated = requestedProjectIds.Except(currentProjectIds).ToList();
        var projectIdsToBeUpdated = GetProjectIdsToBeUpdated(requested);

        var policiesToBeDeleted =
            CreatePolicyUpdates(ProjectGrantedPolicies, projectIdsToBeDeleted, AccessPolicyOperation.Delete);
        var policiesToBeCreated = CreatePolicyUpdates(requested.ProjectGrantedPolicies, projectIdsToBeCreated,
            AccessPolicyOperation.Create);
        var policiesToBeUpdated = CreatePolicyUpdates(requested.ProjectGrantedPolicies, projectIdsToBeUpdated,
            AccessPolicyOperation.Update);

        return new ServiceAccountGrantedPoliciesUpdates
        {
            OrganizationId = OrganizationId,
            ServiceAccountId = ServiceAccountId,
            ProjectGrantedPolicyUpdates =
                policiesToBeDeleted.Concat(policiesToBeCreated).Concat(policiesToBeUpdated)
        };
    }

    private static List<ServiceAccountProjectAccessPolicyUpdate> CreatePolicyUpdates(
        IEnumerable<ServiceAccountProjectAccessPolicy> policies, List<Guid> projectIds,
        AccessPolicyOperation operation) =>
        policies
            .Where(ap => projectIds.Contains(ap.GrantedProjectId!.Value))
            .Select(ap => new ServiceAccountProjectAccessPolicyUpdate { Operation = operation, AccessPolicy = ap })
            .ToList();

    private List<Guid> GetProjectIdsToBeUpdated(ServiceAccountGrantedPolicies requested) =>
        ProjectGrantedPolicies
            .Where(currentAp => requested.ProjectGrantedPolicies.Any(requestedAp =>
                requestedAp.GrantedProjectId == currentAp.GrantedProjectId &&
                requestedAp.ServiceAccountId == currentAp.ServiceAccountId &&
                (requestedAp.Write != currentAp.Write || requestedAp.Read != currentAp.Read)))
            .Select(ap => ap.GrantedProjectId!.Value)
            .ToList();
}

public class ServiceAccountGrantedPoliciesUpdates
{
    public Guid ServiceAccountId { get; set; }
    public Guid OrganizationId { get; set; }

    public IEnumerable<ServiceAccountProjectAccessPolicyUpdate> ProjectGrantedPolicyUpdates { get; set; } =
        new List<ServiceAccountProjectAccessPolicyUpdate>();
}
