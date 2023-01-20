using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretManagerFeatures.AccessPolicies.Interfaces;

namespace Bit.Commercial.Core.SecretManagerFeatures.AccessPolicies;

public class DeleteAccessPolicyCommand : IDeleteAccessPolicyCommand
{
    private readonly IAccessPolicyRepository _accessPolicyRepository;

    public DeleteAccessPolicyCommand(IAccessPolicyRepository accessPolicyRepository)
    {
        _accessPolicyRepository = accessPolicyRepository;
    }


    public async Task DeleteAsync(Guid id)
    {
        var accessPolicy = await _accessPolicyRepository.GetByIdAsync(id);
        if (accessPolicy == null)
        {
            throw new NotFoundException();
        }

        await _accessPolicyRepository.DeleteAsync(id);
    }
}
