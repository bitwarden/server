using Bit.Core.Enums;
using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;

public interface ICreateAccessPoliciesCommand
{
    Task<IEnumerable<BaseAccessPolicy>> CreateManyAsync(List<BaseAccessPolicy> accessPolicies, Guid userId, AccessClientType accessType);
}
