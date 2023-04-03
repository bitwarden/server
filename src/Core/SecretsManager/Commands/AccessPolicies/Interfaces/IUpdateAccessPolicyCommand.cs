using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;

public interface IUpdateAccessPolicyCommand
{
    public Task<BaseAccessPolicy> UpdateAsync(BaseAccessPolicy accessPolicy, bool read, bool write);
}
