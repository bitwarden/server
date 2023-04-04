using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.AccessPolicies;

public class DeleteAccessPolicyCommand : IDeleteAccessPolicyCommand
{
    private readonly IAccessPolicyRepository _accessPolicyRepository;


    public DeleteAccessPolicyCommand(
        IAccessPolicyRepository accessPolicyRepository)
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
