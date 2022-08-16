using Bit.Core.Entities;

namespace Bit.Commercial.Core.SecretManagerFeatures.Secrets.Interfaces
{
    public interface IUpdateSecretCommand
    {
        Task<Secret> UpdateAsync(Secret secret);
    }
}

