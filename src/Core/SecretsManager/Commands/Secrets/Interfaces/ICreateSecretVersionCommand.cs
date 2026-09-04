#nullable enable
using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.Secrets.Interfaces;

public interface ICreateSecretVersionCommand
{
    Task<SecretVersion> CreateAsync(Secret secret, Guid accessClientId);
}
