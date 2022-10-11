using Bit.Core.Entities;

namespace Bit.Core.SecretManagerFeatures.Secrets.Interfaces;

public interface IDeleteSecretCommand
{
    Task<List<Tuple<Secret, string>>> DeleteSecrets(List<Guid> ids);
}

