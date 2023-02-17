using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.Trash.Interfaces;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.Trash;

public class EmptyTrashCommand : IEmptyTrashCommand
{
    private readonly ISecretRepository _secretRepository;

    public EmptyTrashCommand(ISecretRepository secretRepository)
    {
        _secretRepository = secretRepository;
    }

    public async Task EmptyTrash(Guid organizationId, List<Guid> ids)
    {
        var secrets = await _secretRepository.GetManyByOrganizationIdInTrashByIdsAsync(organizationId, ids);

        var missingId = ids.Except(secrets.Select(_ => _.Id)).Any();
        if (missingId)
        {
            throw new NotFoundException();
        }

        await _secretRepository.HardDeleteManyByIdAsync(ids);
    }
}
