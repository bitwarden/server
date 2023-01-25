using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;

public interface ICreateAccessPoliciesCommand
{
    Task<List<BaseAccessPolicy>> CreateAsync(List<BaseAccessPolicy> accessPolicies);
}
