using Bit.Commercial.Core.SecretManagerFeatures.Secrets.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Repositories;

namespace Bit.Commercial.Core.SecretManagerFeatures.Secrets
{
    public class CreateSecretCommand : ICreateSecretCommand
    {
        private readonly ISecretRepository _secretRepository;

        public CreateSecretCommand(ISecretRepository secretRepository)
        {
            _secretRepository = secretRepository;
        }

        public async Task<Secret> CreateAsync(Secret secret)
        {
            return await _secretRepository.CreateAsync(secret);
        }
    }
}

