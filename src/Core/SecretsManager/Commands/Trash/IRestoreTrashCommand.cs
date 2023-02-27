namespace Bit.Core.SecretsManager.Commands.Trash.Interfaces;

public interface IRestoreTrashCommand
{
    Task RestoreTrash(Guid organizationId, List<Guid> ids);
}
