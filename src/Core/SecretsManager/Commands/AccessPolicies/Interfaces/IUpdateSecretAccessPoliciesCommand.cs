#nullable enable
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;

namespace Bit.Core.SecretsManager.Commands.AccessPolicies.Interfaces;

public interface IUpdateSecretAccessPoliciesCommand
{
    Task UpdateAsync(SecretAccessPoliciesUpdates accessPoliciesUpdates);
}
