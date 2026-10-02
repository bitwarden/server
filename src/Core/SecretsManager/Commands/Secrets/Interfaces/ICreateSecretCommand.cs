using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Models.Data.AccessPolicyUpdates;

namespace Bit.Core.SecretsManager.Commands.Secrets.Interfaces;

public interface ICreateSecretCommand
{
    /// <summary>
    /// Creates a secret along with the initial version snapshot of its value, attributed to the
    /// acting client. The snapshot is built here rather than by callers so that no create path can
    /// leave a secret without version history. No snapshot is recorded while the
    /// <see cref="Bit.Core.FeatureFlagKeys.SecretsVersioning"/> feature flag is disabled.
    /// </summary>
    Task<Secret> CreateAsync(Secret secret, SecretAccessPoliciesUpdates? accessPoliciesUpdates);
}
