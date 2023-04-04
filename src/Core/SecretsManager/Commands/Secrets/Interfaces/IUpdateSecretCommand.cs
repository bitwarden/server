using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.Secrets.Interfaces;

public interface IUpdateSecretCommand
{
    Task<Secret> UpdateAsync(Secret updatedSecret);
}
