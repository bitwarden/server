using Bit.Core.SecretsManager.Entities;

namespace Bit.Core.SecretsManager.Commands.Trash.Interfaces;

public interface IRestoreTrashCommand
{
    Task<List<Tuple<Secret, string>>> RestoreTrash(Guid organizationId, List<Guid> ids);
}
