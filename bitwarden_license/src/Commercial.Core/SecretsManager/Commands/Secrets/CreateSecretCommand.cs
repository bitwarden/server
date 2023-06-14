using Bit.Core.SecretsManager.Commands.Secrets.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.Secrets;

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
