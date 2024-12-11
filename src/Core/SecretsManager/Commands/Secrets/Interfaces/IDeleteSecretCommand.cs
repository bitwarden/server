using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.Secrets.Interfaces;

public interface IDeleteSecretCommand
{
    Task DeleteSecrets(IEnumerable<Secret> secrets);
}
