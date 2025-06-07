namespace Bit.Core.SecretsManager.Commands.Trash.Interfaces;

#nullable enable

public interface IRestoreTrashCommand
{
    Task RestoreTrash(Guid organizationId, List<Guid> ids);
}
