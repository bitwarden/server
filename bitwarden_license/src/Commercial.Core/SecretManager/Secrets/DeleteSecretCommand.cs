using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.Secrets.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretManager.Secrets;

public class DeleteSecretCommand : IDeleteSecretCommand
{
    private readonly ISecretRepository _secretRepository;

    public DeleteSecretCommand(ISecretRepository secretRepository)
    {
        _secretRepository = secretRepository;
    }

    public async Task<List<Tuple<Secret, string>>> DeleteSecrets(List<Guid> ids)
    {
        var secrets = await _secretRepository.GetManyByIds(ids);

        if (secrets?.Any() != true)
        {
            throw new NotFoundException();
        }

        var results = ids.Select(id =>
        {
            var secret = secrets.FirstOrDefault(secret => secret.Id == id);
            if (secret == null)
            {
                throw new NotFoundException();
            }
            // TODO Once permissions are implemented add check for each secret here.
            else
            {
                return new Tuple<Secret, string>(secret, "");
            }
        }).ToList();

        await _secretRepository.SoftDeleteManyByIdAsync(ids);
        return results;
    }
}

