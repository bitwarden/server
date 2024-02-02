using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Models.Data;

public class ServiceAccountGrantedPolicies
{
    public Guid ServiceAccountId { get; set; }
    public Guid OrganizationId { get; set; }
    public IEnumerable<ServiceAccountProjectAccessPolicy> ProjectGrantedPolicies { get; set; }

    public IEnumerable<BaseAccessPolicy> ToBaseAccessPolicies()
    {
        var policies = new List<BaseAccessPolicy>();
        if (ProjectGrantedPolicies != null && ProjectGrantedPolicies.Any())
        {
            policies.AddRange(ProjectGrantedPolicies);
        }

        return policies;
    }

    public ProjectIdsOfPolicyChanges GetPolicyChanges(ServiceAccountGrantedPolicies requested)
    {
        var currentProjectIds = ProjectGrantedPolicies.Select(p => p.GrantedProjectId!.Value).ToList();
        var requestedProjectIds = requested.ProjectGrantedPolicies.Select(p => p.GrantedProjectId!.Value).ToList();

        var projectIdsToBeDeleted = currentProjectIds.Except(requestedProjectIds).ToList();
        var projectIdsToBeCreated = requestedProjectIds.Except(currentProjectIds).ToList();
        var projectIdsToBeUpdated = ProjectGrantedPolicies
            .Where(currentAp =>
                requested.ProjectGrantedPolicies.Any(requestedAp =>
                    requestedAp.GrantedProjectId == currentAp.GrantedProjectId &&
                    requestedAp.ServiceAccountId == currentAp.ServiceAccountId &&
                    (requestedAp.Write != currentAp.Write || requestedAp.Read != currentAp.Read)))
            .Select(ap => ap.GrantedProjectId!.Value)
            .ToList();

        return new ProjectIdsOfPolicyChanges(projectIdsToBeCreated, projectIdsToBeDeleted, projectIdsToBeUpdated);
    }
}

public record ProjectIdsOfPolicyChanges(
    IEnumerable<Guid> ProjectIdsToCreate,
    IEnumerable<Guid> ProjectIdsToDelete,
    IEnumerable<Guid> ProjectIdsToUpdate);
