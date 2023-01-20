using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.SecretManagerFeatures.AccessPolicies.Interfaces;

namespace Bit.Commercial.Core.SecretManagerFeatures.AccessPolicies;

public class UpdateAccessPolicyCommand : IUpdateAccessPolicyCommand
{
    private readonly IAccessPolicyRepository _accessPolicyRepository;

    public UpdateAccessPolicyCommand(IAccessPolicyRepository accessPolicyRepository)
    {
        _accessPolicyRepository = accessPolicyRepository;
    }

    public async Task<BaseAccessPolicy> UpdateAsync(Guid id, bool read, bool write)
    {
        var accessPolicy = await _accessPolicyRepository.GetByIdAsync(id);
        if (accessPolicy == null)
        {
            throw new NotFoundException();
        }

        accessPolicy.Read = read;
        accessPolicy.Write = write;
        accessPolicy.RevisionDate = DateTime.UtcNow;

        await _accessPolicyRepository.ReplaceAsync(accessPolicy);
        return accessPolicy;
    }
}
