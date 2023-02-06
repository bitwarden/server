using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.Secrets.Interfaces;

public interface ICreateSecretCommand
{
    Task<Secret> CreateAsync(Secret secret);
}
