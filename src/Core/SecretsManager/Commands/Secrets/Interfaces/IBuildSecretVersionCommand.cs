using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.Secrets.Interfaces;

public interface IBuildSecretVersionCommand
{
    Task<SecretVersion> BuildAsync(Secret secret, Guid accessClientId);
}
