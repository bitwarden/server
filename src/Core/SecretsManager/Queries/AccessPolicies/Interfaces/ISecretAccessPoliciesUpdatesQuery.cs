#nullable enable
using Bit.Core.SecretsManager.Models.Data;
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;

namespace Bit.Core.SecretsManager.Queries.AccessPolicies.Interfaces;

public interface ISecretAccessPoliciesUpdatesQuery
{
    Task<SecretAccessPoliciesUpdates> GetAsync(SecretAccessPolicies accessPolicies, Guid userId);
}
