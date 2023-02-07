using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;

public interface ICreateAccessPoliciesCommand
{
    Task<IEnumerable<BaseAccessPolicy>> CreateForProjectAsync(Guid projectId, List<BaseAccessPolicy> accessPolicies, Guid userId);
}
