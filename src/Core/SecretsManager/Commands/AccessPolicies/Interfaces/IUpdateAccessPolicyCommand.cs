using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;

public interface IUpdateAccessPolicyCommand
{
    public Task<BaseAccessPolicy> UpdateAsync(Guid id, bool read, bool write, Guid userId);
}
