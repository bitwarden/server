#nullable enable
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Enums.AccessPolicies;
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;
using Bit.Core.SecretsManager.Queries.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Queries.AccessPolicies;

public class ServiceAccountGrantedPolicyUpdatesQuery : IServiceAccountGrantedPolicyUpdatesQuery
{
    private readonly IAccessPolicyRepository _accessPolicyRepository;

    public ServiceAccountGrantedPolicyUpdatesQuery(IAccessPolicyRepository accessPolicyRepository)
    {
        _accessPolicyRepository = accessPolicyRepository;
    }

    public async Task<ServiceAccountGrantedPoliciesUpdates> GetAsync(ServiceAccount serviceAccount,
        ServiceAccountGrantedPolicies grantedPolicies)
    {
        var currentPolicies = await _accessPolicyRepository.GetServiceAccountGrantedPoliciesAsync(serviceAccount.Id);
        if (currentPolicies == null)
        {
            return new ServiceAccountGrantedPoliciesUpdates
            {
                ServiceAccountId = serviceAccount.Id,
                OrganizationId = serviceAccount.OrganizationId,
                ProjectGrantedPolicyUpdates = grantedPolicies.ProjectGrantedPolicies.Select(p =>
                    new ServiceAccountProjectAccessPolicyUpdate
                    {
                        Operation = AccessPolicyOperation.Create,
                        AccessPolicy = p
                    })
            };
        }

        return currentPolicies.GetPolicyUpdates(grantedPolicies);
    }
}
