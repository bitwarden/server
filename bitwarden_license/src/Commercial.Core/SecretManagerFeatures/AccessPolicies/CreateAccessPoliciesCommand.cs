#nullable enable
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretManagerFeatures.AccessPolicies.Interfaces;

namespace Bit.Commercial.Core.SecretManagerFeatures.AccessPolicies;

public class CreateAccessPoliciesCommand : ICreateAccessPoliciesCommand
{
    private readonly IAccessPolicyRepository _accessPolicyRepository;

    public CreateAccessPoliciesCommand(IAccessPolicyRepository accessPolicyRepository)
    {
        _accessPolicyRepository = accessPolicyRepository;
    }

    public async Task<List<BaseAccessPolicy>> CreateAsync(List<BaseAccessPolicy> accessPolicies)
    {
        foreach (var accessPolicy in accessPolicies)
        {
            if (await _accessPolicyRepository.AccessPolicyExists(accessPolicy))
            {
                throw new BadRequestException("Resource already exists");
            }
        }

        return await _accessPolicyRepository.CreateManyAsync(accessPolicies);
    }
}
