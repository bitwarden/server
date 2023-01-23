using Bit.Core.Entities;

namespace Bit.Core.SecretManagerFeatures.AccessPolicies.Interfaces;

public interface IUpdateAccessPolicyCommand
{
    public Task<BaseAccessPolicy> UpdateAsync(Guid id, bool read, bool write);
}
