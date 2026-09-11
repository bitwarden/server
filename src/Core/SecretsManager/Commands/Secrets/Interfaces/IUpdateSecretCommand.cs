using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;

namespace Bit.Core.SecretsManager.Commands.Secrets.Interfaces;

public interface IUpdateSecretCommand
{
    /// <summary>
    /// Updates a secret, recording a version snapshot of the new value when
    /// <paramref name="valueChanged"/> is set. The snapshot is built here rather than by callers so
    /// the decision to record one stays with the write itself.
    /// </summary>
    /// <param name="valueChanged">
    /// Whether this update changes the secret's value. An edit that leaves the value alone has
    /// nothing at risk of being lost, so it records no version.
    /// </param>
    Task<Secret> UpdateAsync(Secret secret, SecretAccessPoliciesUpdates? accessPolicyUpdates, bool valueChanged);
}
