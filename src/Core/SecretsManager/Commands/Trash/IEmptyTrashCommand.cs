namespace Bit.Core.SecretsManager.Commands.Trash.Interfaces;

#nullable enable

public interface IEmptyTrashCommand
{
    Task EmptyTrash(Guid organizationId, List<Guid> ids);
}

