namespace Bit.Core.Vault.Commands.Interfaces;

public interface IDeleteManyFoldersCommand
{
    /// <summary>
    /// Deletes the user's folders and re-assigns any ciphers filed under them to no folder.
    /// </summary>
    /// <param name="folderIds">The folders to delete. Ids the user does not own are ignored.</param>
    /// <param name="userId">The owner of the folders.</param>
    Task DeleteManyAsync(IEnumerable<Guid> folderIds, Guid userId);
}
