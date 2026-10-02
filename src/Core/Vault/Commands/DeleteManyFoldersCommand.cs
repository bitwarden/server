using Bit.Core.Exceptions;
using Bit.Core.Platform.Push;
using Bit.Core.Vault.Commands.Interfaces;
using Bit.Core.Vault.Repositories;

namespace Bit.Core.Vault.Commands;

public class DeleteManyFoldersCommand(
    IFolderRepository folderRepository,
    IPushNotificationService pushService)
    : IDeleteManyFoldersCommand
{
    public async Task DeleteManyAsync(IEnumerable<Guid> folderIds, Guid userId)
    {
        var requestedIds = folderIds?.ToHashSet();
        if (requestedIds == null || requestedIds.Count == 0)
        {
            throw new BadRequestException("No folder ids provided.");
        }

        var folders = await folderRepository.GetManyByUserIdAsync(userId);
        var deletingFolders = folders.Where(f => requestedIds.Contains(f.Id)).ToList();

        if (deletingFolders.Count == 0)
        {
            return;
        }

        await folderRepository.DeleteManyAsync(deletingFolders.Select(f => f.Id), userId);

        // Deleting folders also re-assigns the ciphers filed under them, so clients need a full vault sync
        // rather than a per-folder delete notification.
        await pushService.PushSyncVaultAsync(userId);
    }
}
