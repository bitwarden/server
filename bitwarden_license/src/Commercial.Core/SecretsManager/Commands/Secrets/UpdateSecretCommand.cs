using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.Secrets.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.Secrets;

public class UpdateSecretCommand : IUpdateSecretCommand
{
    private readonly ISecretRepository _secretRepository;

    public UpdateSecretCommand(ISecretRepository secretRepository)
    {
        _secretRepository = secretRepository;
    }

    public async Task<Secret> UpdateAsync(Secret updatedSecret)
    {
        var secret = await _secretRepository.GetByIdAsync(updatedSecret.Id);
        if (secret == null)
        {
            throw new NotFoundException();
        }

        secret.Key = updatedSecret.Key;
        secret.Value = updatedSecret.Value;
        secret.Note = updatedSecret.Note;
        secret.Projects = updatedSecret.Projects;
        secret.RevisionDate = DateTime.UtcNow;

        await _secretRepository.UpdateAsync(secret);
        return secret;
    }
}
