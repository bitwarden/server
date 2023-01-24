using Bit.Core.Entities;

namespace Bit.Core.SecretManagerFeatures.Secrets.Interfaces;

public interface ICreateSecretCommand
{
    Task<Secret> CreateAsync(Secret secret, Guid userId);
}
