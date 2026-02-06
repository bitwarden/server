#nullable enable
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;

namespace Bit.Core.SecretsManager.Commands.Secrets.Interfaces;

public interface IUpdateSecretCommand
{
    Task<Secret> UpdateAsync(Secret secret, SecretAccessPoliciesUpdates? accessPolicyUpdates);
}
