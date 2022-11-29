using Bit.Core.Entities;

namespace Bit.Core.SecretManagerFeatures.Secrets.Interfaces;

public interface IUpdateSecretCommand
{
    Task<Secret> UpdateAsync(Secret secret, Guid[]? projectIds);
}
