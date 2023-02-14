using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.Trash.Interfaces;

public interface IEmptyTrashCommand
{
    Task<List<Tuple<Secret, string>>> EmptyTrash(Guid organizationId, List<Guid> ids);
}

