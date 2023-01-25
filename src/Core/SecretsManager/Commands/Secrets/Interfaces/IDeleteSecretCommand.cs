using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.Secrets.Interfaces;

public interface IDeleteSecretCommand
{
    Task<List<Tuple<Secret, string>>> DeleteSecrets(List<Guid> ids);
}

