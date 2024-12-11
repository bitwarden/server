namespace Bit.Core.SecretsManager.Commands.Trash.Interfaces;

public interface IEmptyTrashCommand
{
    Task EmptyTrash(Guid organizationId, List<Guid> ids);
}
