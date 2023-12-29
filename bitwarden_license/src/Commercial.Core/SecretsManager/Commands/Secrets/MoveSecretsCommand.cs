using Bit.Core.SecretsManager.Commands.Secrets.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.Secrets;

public class MoveSecretsCommand : IMoveSecretsCommand
{
    private readonly ISecretRepository _secretRepository;

    public MoveSecretsCommand(ISecretRepository secretRepository)
    {
        _secretRepository = secretRepository;
    }

    public async Task MoveSecretsAsync(IEnumerable<Secret> secrets, Guid project)
    {
        await _secretRepository.MoveSecretsAsync(secrets, project);
    }
}
