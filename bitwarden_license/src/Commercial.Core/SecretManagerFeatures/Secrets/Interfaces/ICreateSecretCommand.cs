using Bit.Core.Entities;

namespace Bit.Commercial.Core.SecretManagerFeatures.Secrets.Interfaces
{
    public interface ICreateSecretCommand
    {
        Task<Secret> CreateAsync(Secret secret);
    }
}

