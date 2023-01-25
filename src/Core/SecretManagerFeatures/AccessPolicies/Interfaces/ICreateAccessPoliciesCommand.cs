using Bit.Core.Entities;

namespace Bit.Core.SecretManagerFeatures.AccessPolicies.Interfaces;

public interface ICreateAccessPoliciesCommand
{
    Task<IEnumerable<BaseAccessPolicy>> CreateForProjectAsync(Guid projectId, List<BaseAccessPolicy> accessPolicies, Guid userId);
}
