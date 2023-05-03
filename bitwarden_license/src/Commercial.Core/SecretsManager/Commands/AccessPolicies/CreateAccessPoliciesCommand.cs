using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.AccessPolicies;

public class CreateAccessPoliciesCommand : ICreateAccessPoliciesCommand
{
    private readonly IAccessPolicyRepository _accessPolicyRepository;

    public CreateAccessPoliciesCommand(IAccessPolicyRepository accessPolicyRepository)
    {
        _accessPolicyRepository = accessPolicyRepository;
    }

    private static List<BaseAccessPolicy> ClearGranteeAndGrantedReferences(List<BaseAccessPolicy> accessPolicies)
    {
        var result = new List<BaseAccessPolicy>();

        foreach (var policy in accessPolicies)
        {
            switch (policy)
            {
                case UserProjectAccessPolicy ap:
                    ap.GrantedProject = null;
                    ap.User = null;
                    result.Add(ap);
                    break;
                case GroupProjectAccessPolicy ap:
                    ap.GrantedProject = null;
                    ap.Group = null;
                    result.Add(ap);
                    break;
                case ServiceAccountProjectAccessPolicy ap:
                    ap.GrantedProject = null;
                    ap.ServiceAccount = null;
                    result.Add(ap);
                    break;
                case UserServiceAccountAccessPolicy ap:
                    ap.GrantedServiceAccount = null;
                    ap.User = null;
                    result.Add(ap);
                    break;
                case GroupServiceAccountAccessPolicy ap:
                    ap.GrantedServiceAccount = null;
                    ap.Group = null;
                    result.Add(ap);
                    break;
                default:
                    throw new ArgumentException("Unsupported access policy type provided.", nameof(policy));
            }
        }

        return result;
    }

    public async Task<IEnumerable<BaseAccessPolicy>> CreateManyAsync(List<BaseAccessPolicy> accessPolicies)
    {
        accessPolicies = ClearGranteeAndGrantedReferences(accessPolicies);
        await CheckAccessPoliciesDoNotExistAsync(accessPolicies);
        return await _accessPolicyRepository.CreateManyAsync(accessPolicies);
    }

    private async Task CheckAccessPoliciesDoNotExistAsync(List<BaseAccessPolicy> accessPolicies)
    {
        foreach (var accessPolicy in accessPolicies)
        {
            if (await _accessPolicyRepository.AccessPolicyExists(accessPolicy))
            {
                throw new BadRequestException("Resource already exists");
            }
        }
    }
}
