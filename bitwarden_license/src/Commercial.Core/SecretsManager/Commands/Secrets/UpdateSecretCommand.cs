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

    public async Task<Secret> UpdateAsync(Secret secret)
    {
        var existingSecret = await _secretRepository.GetByIdAsync(secret.Id);
        if (existingSecret == null)
        {
            throw new NotFoundException();
        }

        secret.OrganizationId = existingSecret.OrganizationId;
        secret.CreationDate = existingSecret.CreationDate;
        secret.DeletedDate = existingSecret.DeletedDate;
        secret.RevisionDate = DateTime.UtcNow;

        await _secretRepository.UpdateAsync(secret);
        return secret;
    }
}
