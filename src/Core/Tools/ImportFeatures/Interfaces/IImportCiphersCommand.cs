using Bit.Core.Entities;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;

namespace Bit.Core.Tools.ImportFeatures.Interfaces;

public interface IImportCiphersCommand
{
    Task ImportIntoIndividualVaultAsync(List<Folder> folders, List<CipherDetails> ciphers,
        IEnumerable<KeyValuePair<int, int>> folderRelationships, Guid importingUserId);

    Task ImportIntoOrganizationalVaultAsync(List<Collection> collections, List<CipherDetails> ciphers,
        IEnumerable<KeyValuePair<int, int>> collectionRelationships, Guid importingUserId);
}
