#nullable enable
using Bit.Core.SecretsManager.Enums.AccessPolicies;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;
using Bit.Core.SecretsManager.Queries.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Queries.AccessPolicies;

public class ProjectServiceAccountsPoliciesUpdatesQuery : IProjectServiceAccountsPoliciesUpdatesQuery
{
    private readonly IAccessPolicyRepository _accessPolicyRepository;

    public ProjectServiceAccountsPoliciesUpdatesQuery(IAccessPolicyRepository accessPolicyRepository)
    {
        _accessPolicyRepository = accessPolicyRepository;
    }

    public async Task<ProjectServiceAccountsPoliciesUpdates> GetAsync(
        ProjectServiceAccountsAccessPolicies projectServiceAccountsAccessPolicies)
    {
        var currentPolicies =
            await _accessPolicyRepository.GetProjectServiceAccountsAccessPoliciesAsync(
                projectServiceAccountsAccessPolicies.ProjectId);

        if (currentPolicies == null)
        {
            return new ProjectServiceAccountsPoliciesUpdates
            {
                ProjectId = projectServiceAccountsAccessPolicies.ProjectId,
                OrganizationId = projectServiceAccountsAccessPolicies.OrganizationId,
                ServiceAccountAccessPolicyUpdates =
                    projectServiceAccountsAccessPolicies.ServiceAccountAccessPolicies.Select(p =>
                        new ServiceAccountProjectAccessPolicyUpdate
                        {
                            Operation = AccessPolicyOperation.Create,
                            AccessPolicy = p
                        })
            };
        }

        return currentPolicies.GetPolicyUpdates(projectServiceAccountsAccessPolicies);
    }
}
