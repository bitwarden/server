using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Queries.Access.Interfaces;

public interface IAccessPolicyAccessQuery
{
    Task<bool> HasAccess(BaseAccessPolicy baseAccessPolicy, Guid userId);
    Task<bool> HasAccess(List<BaseAccessPolicy> baseAccessPolicies, Guid organizationId, Guid userId);
}
