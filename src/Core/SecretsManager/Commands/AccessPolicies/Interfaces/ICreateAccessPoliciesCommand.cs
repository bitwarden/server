using Bit.Core.Enums;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;

public interface ICreateAccessPoliciesCommand
{
    Task<IEnumerable<BaseAccessPolicy>> CreateManyAsync(List<BaseAccessPolicy> accessPolicies, Guid userId, AccessClientType accessType);
    Task<IEnumerable<BaseAccessPolicy>> CreateForProjectAsync(Guid projectId, List<BaseAccessPolicy> accessPolicies, Guid userId);
    Task<IEnumerable<BaseAccessPolicy>> CreateForServiceAccountAsync(Guid serviceAccountId, List<BaseAccessPolicy> accessPolicies, Guid userId);
}
