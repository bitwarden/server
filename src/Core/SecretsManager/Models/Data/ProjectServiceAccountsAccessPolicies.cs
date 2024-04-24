#nullable enable
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Enums.AccessPolicies;
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;

namespace Bit.Core.SecretsManager.Models.Data;

public class ProjectServiceAccountsAccessPolicies
{
    public ProjectServiceAccountsAccessPolicies()
    {
    }

    public ProjectServiceAccountsAccessPolicies(Guid projectId,
        IEnumerable<BaseAccessPolicy> policies)
    {
        ProjectId = projectId;
        ServiceAccountAccessPolicies = policies
            .OfType<ServiceAccountProjectAccessPolicy>()
            .ToList();

        var project = ServiceAccountAccessPolicies.FirstOrDefault()?.GrantedProject;
        if (project != null)
        {
            OrganizationId = project.OrganizationId;
        }
    }

    public Guid ProjectId { get; set; }
    public Guid OrganizationId { get; set; }
    public IEnumerable<ServiceAccountProjectAccessPolicy> ServiceAccountAccessPolicies { get; set; } = [];

    public ProjectServiceAccountsAccessPoliciesUpdates GetPolicyUpdates(ProjectServiceAccountsAccessPolicies requested)
    {
        var currentServiceAccountIds = GetServiceAccountIds(ServiceAccountAccessPolicies);
        var requestedServiceAccountIds = GetServiceAccountIds(requested.ServiceAccountAccessPolicies);

        var serviceAccountIdsToBeDeleted = currentServiceAccountIds.Except(requestedServiceAccountIds).ToList();
        var serviceAccountIdsToBeCreated = requestedServiceAccountIds.Except(currentServiceAccountIds).ToList();
        var serviceAccountIdsToBeUpdated = GetServiceAccountIdsToBeUpdated(requested);

        var policiesToBeDeleted =
            CreatePolicyUpdates(ServiceAccountAccessPolicies, serviceAccountIdsToBeDeleted,
                AccessPolicyOperation.Delete);
        var policiesToBeCreated = CreatePolicyUpdates(requested.ServiceAccountAccessPolicies,
            serviceAccountIdsToBeCreated,
            AccessPolicyOperation.Create);
        var policiesToBeUpdated = CreatePolicyUpdates(requested.ServiceAccountAccessPolicies,
            serviceAccountIdsToBeUpdated,
            AccessPolicyOperation.Update);

        return new ProjectServiceAccountsAccessPoliciesUpdates
        {
            OrganizationId = OrganizationId,
            ProjectId = ProjectId,
            ServiceAccountAccessPolicyUpdates =
                policiesToBeDeleted.Concat(policiesToBeCreated).Concat(policiesToBeUpdated)
        };
    }

    private static List<ServiceAccountProjectAccessPolicyUpdate> CreatePolicyUpdates(
        IEnumerable<ServiceAccountProjectAccessPolicy> policies, List<Guid> serviceAccountIds,
        AccessPolicyOperation operation) =>
        policies
            .Where(ap => serviceAccountIds.Contains(ap.ServiceAccountId!.Value))
            .Select(ap => new ServiceAccountProjectAccessPolicyUpdate { Operation = operation, AccessPolicy = ap })
            .ToList();

    private List<Guid> GetServiceAccountIdsToBeUpdated(ProjectServiceAccountsAccessPolicies requested) =>
        ServiceAccountAccessPolicies
            .Where(currentAp => requested.ServiceAccountAccessPolicies.Any(requestedAp =>
                requestedAp.GrantedProjectId == currentAp.GrantedProjectId &&
                requestedAp.ServiceAccountId == currentAp.ServiceAccountId &&
                (requestedAp.Write != currentAp.Write || requestedAp.Read != currentAp.Read)))
            .Select(ap => ap.ServiceAccountId!.Value)
            .ToList();

    private static List<Guid> GetServiceAccountIds(IEnumerable<ServiceAccountProjectAccessPolicy> policies) =>
        policies.Select(ap => ap.ServiceAccountId!.Value).ToList();
}
