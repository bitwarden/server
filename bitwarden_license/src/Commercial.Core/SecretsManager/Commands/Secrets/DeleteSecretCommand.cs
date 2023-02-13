using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.Secrets.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.Secrets;

public class DeleteSecretCommand : IDeleteSecretCommand
{
    private readonly ICurrentContext _currentContext;
    private readonly ISecretRepository _secretRepository;

    public DeleteSecretCommand(ICurrentContext currentContext, ISecretRepository secretRepository)
    {
        _currentContext = currentContext;
        _secretRepository = secretRepository;
    }

    public async Task<List<Tuple<Secret, string>>> DeleteSecrets(List<Guid> ids)
    {
        var secrets = await _secretRepository.GetManyByIds(ids);

        if (secrets?.Any() != true)
        {
            throw new NotFoundException();
        }

        // Ensure all secrets belongs to the same organization
        var organizationId = secrets.First().OrganizationId;
        if (secrets.Any(p => p.OrganizationId != organizationId))
        {
            throw new BadRequestException();
        }

        if (!_currentContext.AccessSecretsManager(organizationId))
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

