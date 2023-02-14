using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.SecretsManager.Commands.Trash.Interfaces;
using Bit.Core.SecretsManager.Entities;
using Bit.Core.SecretsManager.Repositories;

namespace Bit.Commercial.Core.SecretsManager.Commands.Secrets;

public class EmptyTrashCommand : IEmptyTrashCommand
{
    private readonly ICurrentContext _currentContext;
    private readonly ISecretRepository _secretRepository;

    public EmptyTrashCommand(ICurrentContext currentContext, ISecretRepository secretRepository)
    {
        _currentContext = currentContext;
        _secretRepository = secretRepository;
    }

    public async Task<List<Tuple<Secret, string>>> EmptyTrash(Guid organizationId, List<Guid> ids)
    {
        var secrets = await _secretRepository.GetManyByOrganizationIdInTrashByIdsAsync(organizationId, ids);

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

        await _secretRepository.HardDeleteManyByIdAsync(ids);
        return results;
    }
}
